using System.Text.Json;

namespace GhEntraFederation;

public sealed class CliFacade(IProcessRunner processRunner, CommandBuilder commandBuilder)
{
    private const string AcceptHeader = "Accept: application/vnd.github+json";

    public async Task EnsureToolAsync(string toolName, CancellationToken cancellationToken)
    {
        await RunCheckedAsync(
            toolName,
            ["--version"],
            $"Required tool not found or not executable: {toolName}",
            cancellationToken);
    }

    public async Task EnsureGhAuthenticatedAsync(CancellationToken cancellationToken)
    {
        await RunCheckedAsync(
            "gh",
            ["auth", "status", "--hostname", "github.com"],
            "GitHub CLI is not authenticated for github.com.",
            cancellationToken);
    }

    public async Task<AzureAccount> GetAzureAccountAsync(CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync("az", ["account", "show", "--output", "json"], "Failed to get Azure account context.", cancellationToken);
        var account = JsonSerializer.Deserialize<AzureAccount>(result.StandardOutput) ?? throw new InvalidOperationException("Failed to parse Azure account context.");
        if (!string.Equals(account.EnvironmentName, "AzureCloud", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported Azure cloud: {account.EnvironmentName}. Only AzureCloud is supported.");
        }

        return account;
    }

    public async Task<GitHubRepository> GetRepositoryAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var path = $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}";
        var result = await RunCheckedAsync("gh", ["api", "--hostname", "github.com", "-H", AcceptHeader, path], "Failed to read repository metadata.", cancellationToken);
        return JsonSerializer.Deserialize<GitHubRepository>(result.StandardOutput) ?? throw new InvalidOperationException("Failed to parse repository metadata.");
    }

    public async Task<RepoOidcTemplate> GetRepoOidcTemplateAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var path = $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/actions/oidc/customization/sub";
        var result = await RunCheckedAsync("gh", ["api", "--hostname", "github.com", "-H", AcceptHeader, path], "Failed to read repository OIDC customization.", cancellationToken);
        return JsonSerializer.Deserialize<RepoOidcTemplate>(result.StandardOutput) ?? throw new InvalidOperationException("Failed to parse repository OIDC customization.");
    }

    public async Task<OrgOidcTemplate> GetOrgOidcTemplateAsync(string org, CancellationToken cancellationToken)
    {
        var path = $"/orgs/{Uri.EscapeDataString(org)}/actions/oidc/customization/sub";
        var result = await RunCheckedAsync("gh", ["api", "--hostname", "github.com", "-H", AcceptHeader, path], "Failed to read organization OIDC customization.", cancellationToken);
        return JsonSerializer.Deserialize<OrgOidcTemplate>(result.StandardOutput) ?? throw new InvalidOperationException("Failed to parse organization OIDC customization.");
    }

    public async Task<AppInfo> GetAppByIdAsync(string appId, CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync("az", ["ad", "app", "show", "--id", appId, "--output", "json"], "Failed to read app registration.", cancellationToken);
        return JsonSerializer.Deserialize<AppInfo>(result.StandardOutput) ?? throw new InvalidOperationException("Failed to parse app registration.");
    }

    public async Task<AppInfo> CreateAppAsync(string appName, CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync("az", ["ad", "app", "create", "--display-name", appName, "--output", "json"], "Failed to create app registration.", cancellationToken);
        return JsonSerializer.Deserialize<AppInfo>(result.StandardOutput) ?? throw new InvalidOperationException("Failed to parse created app registration.");
    }

    public async Task<ServicePrincipalInfo?> TryGetServicePrincipalAsync(string appId, CancellationToken cancellationToken)
    {
        var result = await RunAsync("az", ["ad", "sp", "show", "--id", appId, "--output", "json"], cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ServicePrincipalInfo>(result.StandardOutput);
    }

    public async Task<ServicePrincipalInfo> CreateServicePrincipalAsync(string appId, CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync("az", ["ad", "sp", "create", "--id", appId, "--output", "json"], "Failed to create service principal.", cancellationToken);
        return JsonSerializer.Deserialize<ServicePrincipalInfo>(result.StandardOutput) ?? throw new InvalidOperationException("Failed to parse service principal.");
    }

    public async Task<IReadOnlyList<FederatedCredentialInfo>> ListFederatedCredentialsAsync(string appId, CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync("az", ["ad", "app", "federated-credential", "list", "--id", appId, "--output", "json"], "Failed to list federated credentials.", cancellationToken);
        return JsonSerializer.Deserialize<List<FederatedCredentialInfo>>(result.StandardOutput) ?? [];
    }

    public async Task CreateFederatedCredentialAsync(string appId, string credentialName, string subject, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                name = credentialName,
                issuer = "https://token.actions.githubusercontent.com",
                subject,
                audiences = new[] { "api://AzureADTokenExchange" }
            });
        var payloadPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(payloadPath, payload, cancellationToken);
            await RunCheckedAsync(
                "az",
                ["ad", "app", "federated-credential", "create", "--id", appId, "--parameters", payloadPath],
                "Failed to create federated credential.",
                cancellationToken);
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    public async Task<string> GetResourceGroupIdAsync(string resourceGroupName, CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync("az", ["group", "show", "--name", resourceGroupName, "--output", "json"], "Failed to read resource group.", cancellationToken);
        using var json = JsonDocument.Parse(result.StandardOutput);
        return json.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Resource group id was empty.");
    }

    public async Task<IReadOnlyList<RoleAssignmentInfo>> ListRoleAssignmentsAsync(string scope, string principalObjectId, CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync(
            "az",
            ["role", "assignment", "list", "--scope", scope, "--assignee-object-id", principalObjectId, "--role", "Contributor", "--include-inherited", "false", "--all", "--output", "json"],
            "Failed to list role assignments.",
            cancellationToken);
        return JsonSerializer.Deserialize<List<RoleAssignmentInfo>>(result.StandardOutput) ?? [];
    }

    public async Task CreateRoleAssignmentAsync(string scope, string principalObjectId, CancellationToken cancellationToken)
    {
        await RunCheckedAsync(
            "az",
            ["role", "assignment", "create", "--scope", scope, "--assignee-object-id", principalObjectId, "--role", "Contributor", "--output", "json"],
            "Failed to create role assignment.",
            cancellationToken);
    }

    public async Task EnsureEnvironmentAsync(string repoFullName, string environmentName, CancellationToken cancellationToken)
    {
        var path = $"/repos/{repoFullName}/environments/{Uri.EscapeDataString(environmentName)}";
        await RunCheckedAsync(
            "gh",
            ["api", "--hostname", "github.com", "-X", "PUT", "-H", AcceptHeader, path],
            "Failed to create or update GitHub Environment.",
            cancellationToken);
    }

    public async Task SetEnvironmentSecretAsync(string repoFullName, string environmentName, string secretName, string secretValue, CancellationToken cancellationToken)
    {
        await RunCheckedAsync(
            "gh",
            ["secret", "set", secretName, "--repo", repoFullName, "--env", environmentName],
            $"Failed to set GitHub Environment secret: {secretName}",
            cancellationToken,
            secretValue);
    }

    private Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> args, CancellationToken cancellationToken, string? stdin = null)
    {
        var request = commandBuilder.Build(fileName, args, stdin);
        return processRunner.RunAsync(request, cancellationToken);
    }

    private async Task<ProcessResult> RunCheckedAsync(string fileName, IReadOnlyList<string> args, string operationMessage, CancellationToken cancellationToken, string? stdin = null)
    {
        var result = await RunAsync(fileName, args, cancellationToken, stdin);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new CliCommandException(operationMessage, fileName, args, detail.Trim());
        }

        return result;
    }
}

public sealed class CliCommandException(string message, string fileName, IReadOnlyList<string> args, string details) : Exception(message)
{
    public string FileName { get; } = fileName;

    public IReadOnlyList<string> Arguments { get; } = args;

    public string Details { get; } = details;
}
