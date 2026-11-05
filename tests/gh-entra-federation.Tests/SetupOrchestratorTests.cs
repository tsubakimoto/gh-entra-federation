using GhEntraFederation;

using YamlDotNet.RepresentationModel;

namespace gh_entra_federation.Tests;

public sealed class SetupOrchestratorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Setup_UsesImmutableSubject_AndWritesSecretsThroughStdin(bool isWindows, bool createApp)
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(new WindowsCommandDecodingRunner(runner), console, new TestEnvironment(isWindows));

        EnqueuePreflight(
            runner,
            oidcJson: """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":true,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(createApp
            ? Matchers.Command("az", "ad", "app", "create", "--display-name", "myapp", "--output", "json")
            : Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), createApp
            ? new ProcessResult(1, "", "not found")
            : new ProcessResult(0, """{"id":"sp-id"}""", ""));
        if (createApp)
        {
            runner.Enqueue(Matchers.Command("az", "ad", "sp", "create", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        }
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(
            request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"),
            new ProcessResult(0, """{"name":"gh-entra-octo-repo-prod-e188412c"}""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id"), new ProcessResult(0, "", ""));

        var exitCode = await app.RunAsync(
            ["setup", "--repo", "octo/repo", "--environment", "prod", createApp ? "--app-name" : "--app-id", createApp ? "myapp" : "client-id", "--yes"],
            CancellationToken.None);

        Assert.True(exitCode == 0, console.Error.ToString());
        Assert.Contains("repo:octo@101/repo@202:environment:prod", console.Out.ToString());
        Assert.All(
            runner.Calls.Where(c => c.FileName == "gh" && c.Arguments.Count > 1 && c.Arguments[0] == "secret"),
            c => Assert.NotNull(c.StandardInput));
    }

    [Fact]
    public async Task Setup_ResolvesOrgTemplate_WhenRepoOptsInWithoutExplicitClaims()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(
            runner,
            oidcJson: """{"use_default":false}""",
            orgOidcJson: """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(
            request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"),
            new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id"), new ProcessResult(0, "", ""));

        var exitCode = await app.RunAsync(
            ["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"],
            CancellationToken.None);

        Assert.True(exitCode == 0, console.Error.ToString());
        Assert.Contains("repo:octo/repo:environment:prod", console.Out.ToString());
    }

    [Fact]
    public async Task Setup_UseDefaultTrue_DoesNotApplyOrgTemplate()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(
            runner,
            oidcJson: """{"use_default":true,"use_immutable_subject":false}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(
            request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"),
            new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id"), new ProcessResult(0, "", ""));

        var exitCode = await app.RunAsync(
            ["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"],
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Count > 0 && c.Arguments.Contains("/orgs/octo/actions/oidc/customization/sub"));
        Assert.Contains("repo:octo/repo:environment:prod", console.Out.ToString());
    }

    [Fact]
    public async Task Setup_RejectsUnsupportedClaimKeys()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(
            runner,
            oidcJson: """{"use_default":false,"include_claim_keys":["repo","actor"],"use_immutable_subject":false}""");

        var exitCode = await app.RunAsync(
            ["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"],
            CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Unsupported OIDC subject customization", console.Error.ToString());
    }

    [Fact]
    public async Task Setup_EncodesEnvironmentColon_InSubject()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(
            runner,
            oidcJson: """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(
            request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"),
            new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod%3Ablue"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id", "prod:blue"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id", "prod:blue"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id", "prod:blue"), new ProcessResult(0, "", ""));

        var exitCode = await app.RunAsync(
            ["setup", "--repo", "octo/repo", "--environment", "prod:blue", "--app-id", "client-id", "--yes"],
            CancellationToken.None);

        Assert.True(exitCode == 0, console.Error.ToString());
        Assert.Contains("environment:prod%3Ablue", console.Out.ToString());
    }

    [Fact]
    public async Task Setup_StopsOnWriteFailure_AndShowsResumeHint()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(
            runner,
            oidcJson: """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(
            request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"),
            new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(1, "", "denied"));

        var exitCode = await app.RunAsync(
            ["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"],
            CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("--app-id client-id", console.Error.ToString());
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Contains("AZURE_TENANT_ID"));
    }

    [Fact]
    public async Task Setup_PrintWorkflowYaml_ThatParses()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(
            runner,
            oidcJson: """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(
            request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"),
            new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id"), new ProcessResult(0, "", ""));

        var exitCode = await app.RunAsync(
            ["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"],
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        var yamlText = ExtractWorkflowYaml(console.Out.ToString());
        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlText));
        Assert.Contains("id-token: write", yamlText);
        Assert.Contains("environment: prod", yamlText);
    }

    [Fact]
    public async Task Setup_FailsWhenAppCreateFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "create", "--display-name", "myapp", "--output", "json"), new ProcessResult(1, "", "failed"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-name", "myapp", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Contains("ad") && c.Arguments.Contains("sp"));
    }

    [Fact]
    public async Task Setup_FailsWhenServicePrincipalCreateFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(1, "", "not found"));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "create", "--id", "client-id", "--output", "json"), new ProcessResult(1, "", "denied"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Contains("federated-credential"));
    }

    [Fact]
    public async Task Setup_FailsWhenFederatedCredentialCreateFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"), new ProcessResult(1, "", "failed"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.FileName == "gh" && c.Arguments.Contains("environments"));
    }

    [Fact]
    public async Task Setup_FailsWhenRoleAssignmentCreateFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "group", "show", "--name", "rg1", "--output", "json"), new ProcessResult(0, """{"id":"/subscriptions/sub-id/resourceGroups/rg1"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("az", "role", "assignment", "list", "--scope", "/subscriptions/sub-id/resourceGroups/rg1", "--assignee-object-id", "sp-id", "--role", "Contributor", "--include-inherited", "false", "--all", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(Matchers.Command("az", "role", "assignment", "create", "--scope", "/subscriptions/sub-id/resourceGroups/rg1", "--assignee-object-id", "sp-id", "--role", "Contributor", "--output", "json"), new ProcessResult(1, "", "failed"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--resource-group", "rg1", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.FileName == "gh" && c.Arguments.Contains("environments"));
    }

    [Fact]
    public async Task Setup_FailsWhenEnvironmentWriteFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(1, "", "failed"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.FileName == "gh" && c.Arguments.Count > 1 && c.Arguments[0] == "secret");
    }

    [Fact]
    public async Task Setup_FailsWhenSecondSecretFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueueSuccessUntilSecrets(runner);
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(1, "", "failed"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Contains("AZURE_SUBSCRIPTION_ID"));
    }

    [Fact]
    public async Task Setup_FailsWhenThirdSecretFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueueSuccessUntilSecrets(runner);
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id"), new ProcessResult(1, "", "failed"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task Setup_FailsForInheritedOrgTemplate_WhenOrgTemplateReadFails()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        runner.Enqueue(Matchers.Command("gh", "--version"), new ProcessResult(0, "gh version", ""));
        runner.Enqueue(Matchers.Command("az", "--version"), new ProcessResult(0, "az version", ""));
        runner.Enqueue(Matchers.Command("gh", "auth", "status", "--hostname", "github.com"), new ProcessResult(0, "", ""));
        runner.Enqueue(Matchers.Command("az", "account", "show", "--output", "json"), new ProcessResult(0, """{"tenantId":"tenant-id","id":"sub-id","environmentName":"AzureCloud"}""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo"), new ProcessResult(0, """{"full_name":"octo/repo","name":"repo","id":202,"owner":{"login":"octo","id":101,"type":"Organization"}}""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/actions/oidc/customization/sub"), new ProcessResult(0, """{"use_default":false}""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/orgs/octo/actions/oidc/customization/sub"), new ProcessResult(1, "", "forbidden"));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);
        Assert.NotEqual(0, exitCode);
        Assert.Contains("organization OIDC customization", console.Error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Setup_FailsForUseDefaultWhenImmutableMissing()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        runner.Enqueue(Matchers.Command("gh", "--version"), new ProcessResult(0, "gh version", ""));
        runner.Enqueue(Matchers.Command("az", "--version"), new ProcessResult(0, "az version", ""));
        runner.Enqueue(Matchers.Command("gh", "auth", "status", "--hostname", "github.com"), new ProcessResult(0, "", ""));
        runner.Enqueue(Matchers.Command("az", "account", "show", "--output", "json"), new ProcessResult(0, """{"tenantId":"tenant-id","id":"sub-id","environmentName":"AzureCloud"}""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo"), new ProcessResult(0, """{"full_name":"octo/repo","name":"repo","id":202,"owner":{"login":"octo","id":101,"type":"Organization"}}""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/actions/oidc/customization/sub"), new ProcessResult(0, """{"use_default":true}""", ""));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);
        Assert.NotEqual(0, exitCode);
        Assert.Contains("immutability", console.Error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Setup_FailsForFicNameCollisionWithDifferentSubject()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, """[{"name":"gh-entra-octo-repo-prod-e188412c","issuer":"https://token.actions.githubusercontent.com","subject":"repo:octo/repo:environment:other","audiences":["api://AzureADTokenExchange"]}]""", ""));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);
        Assert.NotEqual(0, exitCode);
        Assert.Contains("different settings", console.Error.ToString());
    }

    [Fact]
    public async Task Setup_SkipsIdenticalFederatedCredential()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, """[{"name":"gh-entra-octo-repo-prod-e188412c","issuer":"https://token.actions.githubusercontent.com","subject":"repo:octo/repo:environment:prod","audiences":["api://AzureADTokenExchange"]}]""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id"), new ProcessResult(0, "", ""));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Contains("federated-credential") && c.Arguments.Contains("create"));
    }

    [Fact]
    public async Task Setup_FailsOnConflictingFederatedCredential()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, """[{"name":"other","issuer":"https://token.actions.githubusercontent.com","subject":"repo:octo/repo:environment:prod","audiences":["api://AzureADTokenExchange"]}]""", ""));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Conflicting federated credential", console.Error.ToString());
    }

    [Fact]
    public async Task Setup_SkipsExistingExactRoleAssignment()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "group", "show", "--name", "rg1", "--output", "json"), new ProcessResult(0, """{"id":"/subscriptions/sub-id/resourceGroups/rg1"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(
            request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"),
            new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("az", "role", "assignment", "list", "--scope", "/subscriptions/sub-id/resourceGroups/rg1", "--assignee-object-id", "sp-id", "--role", "Contributor", "--include-inherited", "false", "--all", "--output", "json"), new ProcessResult(0, """[{"scope":"/subscriptions/sub-id/resourceGroups/rg1","principalId":"sp-id","roleDefinitionName":"Contributor"}]""", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(SecretMatcher("AZURE_CLIENT_ID", "client-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_TENANT_ID", "tenant-id"), new ProcessResult(0, "", ""));
        runner.Enqueue(SecretMatcher("AZURE_SUBSCRIPTION_ID", "sub-id"), new ProcessResult(0, "", ""));

        var exitCode = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--resource-group", "rg1", "--yes"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.SequenceEqual(["role", "assignment", "create", "--scope", "/subscriptions/sub-id/resourceGroups/rg1", "--assignee-object-id", "sp-id", "--role", "Contributor", "--output", "json"]));
    }

    private sealed class WindowsCommandDecodingRunner(FakeProcessRunner inner) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            if (request.FileName == "cmd.exe")
            {
                Assert.NotNull(request.WindowsCommandLine);
                Assert.NotNull(request.EnvironmentVariables);
                var arguments = Enumerable.Range(0, request.EnvironmentVariables.Count)
                    .Select(index => request.EnvironmentVariables[$"GH_ENTRA_AZ_ARG_{index}"]).ToArray();
                request = new ProcessRequest("az", arguments, request.StandardInput);
            }

            return inner.RunAsync(request, cancellationToken);
        }
    }

    private static string ExtractWorkflowYaml(string output)
    {
        var marker = "Workflow example:";
        var index = output.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? output[(index + marker.Length)..].Trim() : output;
    }

    private static Func<ProcessRequest, bool> SecretMatcher(string secretName, string value, string environment = "prod")
    {
        return request =>
            request.FileName == "gh"
            && request.Arguments.SequenceEqual(["secret", "set", secretName, "--repo", "octo/repo", "--env", environment])
            && request.StandardInput == value;
    }

    private static void EnqueuePreflight(FakeProcessRunner runner, string oidcJson, string? orgOidcJson = null)
    {
        runner.Enqueue(Matchers.Command("gh", "--version"), new ProcessResult(0, "gh version", ""));
        runner.Enqueue(Matchers.Command("az", "--version"), new ProcessResult(0, "az version", ""));
        runner.Enqueue(Matchers.Command("gh", "auth", "status", "--hostname", "github.com"), new ProcessResult(0, "", ""));
        runner.Enqueue(Matchers.Command("az", "account", "show", "--output", "json"), new ProcessResult(0, """{"tenantId":"tenant-id","id":"sub-id","environmentName":"AzureCloud"}""", ""));
        runner.Enqueue(
            Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo"),
            new ProcessResult(0, """{"full_name":"octo/repo","name":"repo","id":202,"owner":{"login":"octo","id":101,"type":"Organization"}}""", ""));
        runner.Enqueue(
            Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/actions/oidc/customization/sub"),
            new ProcessResult(0, oidcJson, ""));

        if (orgOidcJson is not null)
        {
            runner.Enqueue(
                Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/orgs/octo/actions/oidc/customization/sub"),
                new ProcessResult(0, orgOidcJson, ""));
        }
    }

    private static void EnqueueSuccessUntilSecrets(FakeProcessRunner runner)
    {
        EnqueuePreflight(runner, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""");
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-id"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "federated-credential", "list", "--id", "client-id", "--output", "json"), new ProcessResult(0, "[]", ""));
        runner.Enqueue(request => request.FileName == "az" && request.Arguments.Contains("federated-credential") && request.Arguments.Contains("create"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "api", "--hostname", "github.com", "-X", "PUT", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/environments/prod"), new ProcessResult(0, "{}", ""));
    }
}
