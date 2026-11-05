using GhEntraFederation;

namespace gh_entra_federation.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task Setup_RejectsNonInteractiveWithoutYes_BeforeWrites()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: false);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        var code = await app.RunAsync(
            [
                "setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id"
            ],
            CancellationToken.None);

        Assert.NotEqual(0, code);
        Assert.DoesNotContain(runner.Calls, call => call.FileName == "az" && call.Arguments.Contains("create"));
    }

    [Fact]
    public async Task Setup_ConfirmationRejected_StopsBeforeWrites()
    {
        var runner = SetupHappyPathRunner();
        var console = new FakeConsole(isInteractive: true, confirmResult: false);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        var code = await app.RunAsync(
            [
                "setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id"
            ],
            CancellationToken.None);

        Assert.NotEqual(0, code);
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.Contains("federated-credential"));
    }

    [Fact]
    public async Task Setup_FailsWhenGhMissing_BeforeWrites()
    {
        var runner = new FakeProcessRunner();
        runner.Enqueue(Matchers.Command("gh", "--version"), new ProcessResult(1, "", "missing"));
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        var code = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, code);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Contains("federated-credential"));
    }

    [Fact]
    public async Task Setup_FailsWhenGhNotAuthenticated_BeforeWrites()
    {
        var runner = new FakeProcessRunner();
        runner.Enqueue(Matchers.Command("gh", "--version"), new ProcessResult(0, "gh", ""));
        runner.Enqueue(Matchers.Command("az", "--version"), new ProcessResult(0, "az", ""));
        runner.Enqueue(Matchers.Command("gh", "auth", "status", "--hostname", "github.com"), new ProcessResult(1, "", "not logged in"));
        var console = new FakeConsole(isInteractive: true, confirmResult: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        var code = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);

        Assert.NotEqual(0, code);
        Assert.DoesNotContain(runner.Calls, c => c.FileName == "az" && c.Arguments.Contains("ad"));
    }

    [Fact]
    public async Task Help_And_Version_AreAvailable()
    {
        var runner = new FakeProcessRunner();
        var console = new FakeConsole(isInteractive: true);
        var app = new CliApplication(runner, console, new TestEnvironment(isWindows: false));

        var helpCode = await app.RunAsync(["--help"], CancellationToken.None);
        var versionCode = await app.RunAsync(["--version"], CancellationToken.None);

        Assert.Equal(0, helpCode);
        Assert.Equal(0, versionCode);
        Assert.Contains("gh entra-federation setup", console.Out.ToString());
        Assert.Contains("0.1.0", console.Out.ToString());
    }

    [Fact]
    public void NormalizeVersion_StripsBuildMetadataSuffix()
    {
        Assert.Equal("1.2.3", CliApplication.NormalizeVersion("1.2.3+9c0ffee"));
        Assert.Equal("1.2.3-rc.1", CliApplication.NormalizeVersion("1.2.3-rc.1+sha.abcdef"));
    }

    [Fact]
    public async Task Setup_WhenCanceled_Returns130()
    {
        var app = new CliApplication(new CancelingProcessRunner(), new FakeConsole(isInteractive: true), new TestEnvironment(isWindows: false));
        var code = await app.RunAsync(["setup", "--repo", "octo/repo", "--environment", "prod", "--app-id", "client-id", "--yes"], CancellationToken.None);
        Assert.Equal(130, code);
    }

    private static FakeProcessRunner SetupHappyPathRunner()
    {
        var runner = new FakeProcessRunner();
        runner.Enqueue(Matchers.Command("gh", "--version"), new ProcessResult(0, "gh version", ""));
        runner.Enqueue(Matchers.Command("az", "--version"), new ProcessResult(0, "{}", ""));
        runner.Enqueue(Matchers.Command("gh", "auth", "status", "--hostname", "github.com"), new ProcessResult(0, "", ""));
        runner.Enqueue(Matchers.Command("az", "account", "show", "--output", "json"), new ProcessResult(0, """{"tenantId":"t1","id":"s1","environmentName":"AzureCloud"}""", ""));
        runner.Enqueue(
            Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo"),
            new ProcessResult(0, """{"full_name":"octo/repo","name":"repo","id":42,"owner":{"login":"octo","id":7,"type":"Organization"}}""", ""));
        runner.Enqueue(
            Matchers.Command("gh", "api", "--hostname", "github.com", "-H", "Accept: application/vnd.github+json", "/repos/octo/repo/actions/oidc/customization/sub"),
            new ProcessResult(0, """{"use_default":false,"include_claim_keys":["repo","context"],"use_immutable_subject":false,"sub_claim_prefix":"repo"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "app", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"appId":"client-id","id":"app-object"}""", ""));
        runner.Enqueue(Matchers.Command("az", "ad", "sp", "show", "--id", "client-id", "--output", "json"), new ProcessResult(0, """{"id":"sp-object"}""", ""));
        return runner;
    }
}
