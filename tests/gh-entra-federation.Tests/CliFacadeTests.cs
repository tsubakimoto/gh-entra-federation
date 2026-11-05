using System.Text.Json;

using GhEntraFederation;

namespace gh_entra_federation.Tests;

public sealed class CliFacadeTests
{
    [Fact]
    public async Task CreateFederatedCredential_WindowsBatchFixture_ReadsJsonFileThroughNativeProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string subject = "repo:octo@101/repo@202:environment:production";
        var fixtureDirectory = Path.Combine(Path.GetTempPath(), $"gh entra credential fixture {Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, "az.cmd"), $"@echo off\r\n\"{powershell}\" -NoProfile -File \"%~dp0read-parameters.ps1\" %*\r\n");
            await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, "read-parameters.ps1"), """
                if ($args.Count -ne 8 -or $args[6] -ne '--parameters') { throw 'Unexpected arguments' }
                [Console]::WriteLine([IO.File]::ReadAllText($args[7]))
                """);
            var runner = new FixtureProcessRunner(fixtureDirectory);
            var facade = new CliFacade(runner, new CommandBuilder(new TestEnvironment(isWindows: true)));

            await facade.CreateFederatedCredentialAsync("client-id", "credential-name", subject, CancellationToken.None);

            Assert.Equal(0, runner.Result.ExitCode);
            using var json = JsonDocument.Parse(runner.Result.StandardOutput);
            Assert.Equal("credential-name", json.RootElement.GetProperty("name").GetString());
            Assert.Equal(subject, json.RootElement.GetProperty("subject").GetString());
            Assert.NotNull(runner.PayloadPath);
            Assert.False(File.Exists(runner.PayloadPath));
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    private sealed class FixtureProcessRunner(string fixtureDirectory) : IProcessRunner
    {
        public ProcessResult Result { get; private set; }
        public string? PayloadPath { get; private set; }

        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            PayloadPath = request.EnvironmentVariables!["GH_ENTRA_AZ_ARG_7"];
            var environment = new Dictionary<string, string>(request.EnvironmentVariables)
            {
                ["PATH"] = $"{fixtureDirectory};{Environment.GetEnvironmentVariable("PATH")}"
            };
            Result = await new SystemProcessRunner().RunAsync(request with { EnvironmentVariables = environment }, cancellationToken);
            return Result;
        }
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    [InlineData(false, 2)]
    public async Task CreateFederatedCredential_UsesJsonFile_AndAlwaysDeletesIt(bool isWindows, int outcome)
    {
        const string subject = "repo:octo@101/repo@202:environment:prod%3Ablue & \"日本語\"";
        string? payloadPath = null;
        var runner = new FakeProcessRunner();
        runner.Enqueue(request =>
        {
            var arguments = isWindows
                ? Enumerable.Range(0, 8).Select(index => request.EnvironmentVariables![$"GH_ENTRA_AZ_ARG_{index}"]).ToArray()
                : request.Arguments.ToArray();
            Assert.Equal(isWindows ? "cmd.exe" : "az", request.FileName);
            Assert.Equal(new[] { "ad", "app", "federated-credential", "create", "--id", "client-id", "--parameters" }, arguments[..7]);
            payloadPath = arguments[7];
            Assert.True(Path.IsPathFullyQualified(payloadPath));
            Assert.True(File.Exists(payloadPath));
            Assert.Null(request.StandardInput);
            using var json = JsonDocument.Parse(File.ReadAllText(payloadPath));
            Assert.Equal("credential-name", json.RootElement.GetProperty("name").GetString());
            Assert.Equal("https://token.actions.githubusercontent.com", json.RootElement.GetProperty("issuer").GetString());
            Assert.Equal(subject, json.RootElement.GetProperty("subject").GetString());
            Assert.Equal(new[] { "api://AzureADTokenExchange" }, json.RootElement.GetProperty("audiences").EnumerateArray().Select(value => value.GetString()).ToArray());
            if (outcome == 2)
            {
                throw new OperationCanceledException();
            }

            return true;
        }, new ProcessResult(outcome, "{}", outcome == 1 ? "denied" : ""));
        var facade = new CliFacade(runner, new CommandBuilder(new TestEnvironment(isWindows)));

        Task CreateAsync() => facade.CreateFederatedCredentialAsync("client-id", "credential-name", subject, CancellationToken.None);
        if (outcome == 1)
        {
            var error = await Assert.ThrowsAsync<CliCommandException>(CreateAsync);
            Assert.Equal("denied", error.Details);
        }
        else if (outcome == 2)
        {
            await Assert.ThrowsAsync<OperationCanceledException>(CreateAsync);
        }
        else
        {
            await CreateAsync();
        }

        Assert.Single(runner.Calls);
        Assert.NotNull(payloadPath);
        Assert.False(File.Exists(payloadPath));
    }
}
