namespace GhEntraFederation;

public sealed class SetupOrchestrator(CliFacade cli, IConsole console)
{
    public async Task<int> RunAsync(SetupOptions options, CancellationToken cancellationToken)
    {
        var created = new SetupCreatedResources();
        try
        {
            ValidateInteraction(options, console);

            await cli.EnsureToolAsync("gh", cancellationToken);
            await cli.EnsureToolAsync("az", cancellationToken);
            await cli.EnsureGhAuthenticatedAsync(cancellationToken);
            var account = await cli.GetAzureAccountAsync(cancellationToken);

            var (owner, repoName) = ParseRepo(options.Repo);
            var repository = await cli.GetRepositoryAsync(owner, repoName, cancellationToken);
            if (!string.Equals(repository.FullName, options.Repo, StringComparison.Ordinal))
            {
                options = options with { Repo = repository.FullName };
            }

            var repoTemplate = await cli.GetRepoOidcTemplateAsync(repository.Owner.Login, repository.Name, cancellationToken);
            OrgOidcTemplate? orgTemplate = null;
            var needsOrgTemplate = !repoTemplate.UseDefault
                && (repoTemplate.IncludeClaimKeys is null || repoTemplate.IncludeClaimKeys.Length == 0)
                && string.Equals(repository.Owner.Type, "Organization", StringComparison.OrdinalIgnoreCase);
            if (needsOrgTemplate)
            {
                orgTemplate = await cli.GetOrgOidcTemplateAsync(repository.Owner.Login, cancellationToken);
            }

            var oidc = OidcSubjectResolver.Resolve(repository, options.Environment, repoTemplate, orgTemplate);
            var existingApp = options.AppId is not null ? await cli.GetAppByIdAsync(options.AppId, cancellationToken) : null;
            string? resourceGroupScope = null;
            if (!string.IsNullOrWhiteSpace(options.ResourceGroup))
            {
                resourceGroupScope = await cli.GetResourceGroupIdAsync(options.ResourceGroup, cancellationToken);
            }

            WritePlan(options, account, oidc, existingApp is null ? "create" : "reuse", resourceGroupScope is not null);
            if (!options.Yes)
            {
                var accepted = await console.ConfirmAsync("Proceed with setup?", cancellationToken);
                if (!accepted)
                {
                    throw new InvalidOperationException("Setup canceled by user.");
                }
            }

            var app = existingApp ?? await cli.CreateAppAsync(options.AppName!, cancellationToken);
            if (existingApp is null)
            {
                created.AppId = app.AppId;
            }

            var servicePrincipal = await EnsureServicePrincipalAsync(app.AppId, cancellationToken);
            created.ServicePrincipalId = servicePrincipal.Id;

            var credentialName = CredentialNameGenerator.Generate(repository.Owner.Login, repository.Name, options.Environment);
            var existingCredentials = await cli.ListFederatedCredentialsAsync(app.AppId, cancellationToken);
            var existingByName = existingCredentials.FirstOrDefault(x => string.Equals(x.Name, credentialName, StringComparison.Ordinal));
            if (existingByName is not null)
            {
                var identical = string.Equals(existingByName.Issuer, "https://token.actions.githubusercontent.com", StringComparison.Ordinal)
                    && string.Equals(existingByName.Subject, oidc.Subject, StringComparison.Ordinal)
                    && existingByName.Audiences.SequenceEqual(["api://AzureADTokenExchange"], StringComparer.Ordinal);
                if (!identical)
                {
                    throw new InvalidOperationException($"Federated credential '{credentialName}' already exists with different settings.");
                }
            }
            else
            {
                var conflict = existingCredentials.Any(
                    x => string.Equals(x.Subject, oidc.Subject, StringComparison.Ordinal)
                         && string.Equals(x.Issuer, "https://token.actions.githubusercontent.com", StringComparison.Ordinal));
                if (conflict)
                {
                    throw new InvalidOperationException("Conflicting federated credential already exists for the same subject.");
                }

                await cli.CreateFederatedCredentialAsync(app.AppId, credentialName, oidc.Subject, cancellationToken);
                created.FederatedCredentialName = credentialName;
            }

            if (resourceGroupScope is not null)
            {
                var assignments = await cli.ListRoleAssignmentsAsync(resourceGroupScope, servicePrincipal.Id, cancellationToken);
                var exists = assignments.Any(
                    x => string.Equals(x.Scope, resourceGroupScope, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(x.PrincipalId, servicePrincipal.Id, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(x.RoleDefinitionName, "Contributor", StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    await cli.CreateRoleAssignmentAsync(resourceGroupScope, servicePrincipal.Id, cancellationToken);
                    created.RoleAssignmentScope = resourceGroupScope;
                }
            }

            await cli.EnsureEnvironmentAsync(repository.FullName, options.Environment, cancellationToken);
            await cli.SetEnvironmentSecretAsync(repository.FullName, options.Environment, "AZURE_CLIENT_ID", app.AppId, cancellationToken);
            await cli.SetEnvironmentSecretAsync(repository.FullName, options.Environment, "AZURE_TENANT_ID", account.TenantId, cancellationToken);
            await cli.SetEnvironmentSecretAsync(repository.FullName, options.Environment, "AZURE_SUBSCRIPTION_ID", account.SubscriptionId, cancellationToken);

            console.WriteLine("Setup completed.");
            console.WriteLine($"Repository: {repository.FullName}");
            console.WriteLine($"App ID: {app.AppId}");
            console.WriteLine($"Service Principal ID: {servicePrincipal.Id}");
            console.WriteLine($"OIDC subject: {oidc.Subject}");
            console.WriteLine("Workflow example:");
            console.WriteLine(WorkflowExampleRenderer.Render(options.Environment));
            return 0;
        }
        catch (CliCommandException ex)
        {
            WriteFailure(ex.Message, ex.Details, options, created);
            return 1;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            WriteFailure(ex.Message, null, options, created);
            return 1;
        }
    }

    private void WritePlan(SetupOptions options, AzureAccount account, OidcResolution oidcResolution, string appOperation, bool hasRbac)
    {
        console.WriteLine($"Repository: {options.Repo}");
        console.WriteLine($"Environment: {options.Environment}");
        console.WriteLine($"Tenant: {account.TenantId}");
        console.WriteLine($"Subscription: {account.SubscriptionId}");
        console.WriteLine($"App operation: {appOperation}");
        console.WriteLine($"OIDC subject: {oidcResolution.Subject}");
        console.WriteLine($"RBAC Contributor assignment: {(hasRbac ? "yes" : "no")}");
        console.WriteLine("Environment secrets to set: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID");
    }

    private void WriteFailure(string message, string? details, SetupOptions options, SetupCreatedResources created)
    {
        console.WriteErrorLine($"Setup failed: {message}");
        if (!string.IsNullOrWhiteSpace(details))
        {
            console.WriteErrorLine(details);
        }

        if (created.AppId is not null)
        {
            console.WriteErrorLine($"Created app id: {created.AppId}");
        }

        if (created.ServicePrincipalId is not null)
        {
            console.WriteErrorLine($"Created service principal id: {created.ServicePrincipalId}");
        }

        if (created.FederatedCredentialName is not null)
        {
            console.WriteErrorLine($"Created federated credential: {created.FederatedCredentialName}");
        }

        console.WriteErrorLine($"Resume with: gh entra-federation setup --repo {options.Repo} --environment {options.Environment} --app-id {(created.AppId ?? options.AppId ?? "<client-id>")} {(options.ResourceGroup is null ? string.Empty : $"--resource-group {options.ResourceGroup}")} --yes");
    }

    private static void ValidateInteraction(SetupOptions options, IConsole console)
    {
        if (!options.Yes && !console.IsInteractive)
        {
            throw new InvalidOperationException("Non-interactive execution requires --yes.");
        }
    }

    private static (string Owner, string Repo) ParseRepo(string fullName)
    {
        var segments = fullName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            throw new ArgumentException("--repo must be OWNER/REPO");
        }

        return (segments[0], segments[1]);
    }

    private async Task<ServicePrincipalInfo> EnsureServicePrincipalAsync(string appId, CancellationToken cancellationToken)
    {
        var existing = await cli.TryGetServicePrincipalAsync(appId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await cli.CreateServicePrincipalAsync(appId, cancellationToken);
    }

    private sealed class SetupCreatedResources
    {
        public string? AppId { get; set; }

        public string? ServicePrincipalId { get; set; }

        public string? FederatedCredentialName { get; set; }

        public string? RoleAssignmentScope { get; set; }
    }
}
