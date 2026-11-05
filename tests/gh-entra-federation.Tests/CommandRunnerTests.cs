using GhEntraFederation;

namespace gh_entra_federation.Tests;

public sealed class CommandRunnerTests
{
    [Fact]
    public void Runner_UsesCmdWrapperForAzOnWindows()
    {
        var commandBuilder = new CommandBuilder(new TestEnvironment(isWindows: true));
        var request = commandBuilder.Build("az", ["group", "show", "--name", "rg&1"]);

        Assert.Equal("cmd.exe", request.FileName);
        Assert.Empty(request.Arguments);
        Assert.Equal("/d /v:off /s /c \"az.cmd \"%GH_ENTRA_AZ_ARG_0%\" \"%GH_ENTRA_AZ_ARG_1%\" \"%GH_ENTRA_AZ_ARG_2%\" \"%GH_ENTRA_AZ_ARG_3%\"\"", request.WindowsCommandLine);
        Assert.Equal("rg&1", request.EnvironmentVariables!["GH_ENTRA_AZ_ARG_3"]);
    }

    [Fact]
    public async Task Cancellation_ThrowsOperationCanceled()
    {
        var runner = new SystemProcessRunner();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunAsync(new ProcessRequest("dotnet", ["--info"]), cts.Token));
    }

    [Fact]
    public async Task WindowsAzCmdWrapper_PassesVersionOptionWithoutLiteralQuotes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = await RunAzFixtureAsync(["--version"]);

        Assert.Equal(0, result.ExitCode);
        var actual = System.Text.Json.JsonSerializer.Deserialize<string[]>(result.StandardOutput);
        Assert.NotNull(actual);
        Assert.Equal(new[] { "--version" }, actual);
    }

    [Fact]
    public async Task WindowsAzCmdWrapper_PreservesHostileArguments_WithLocalFixture()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string[] arguments = ["group", "show", "--name", "rg&A%25!", "%PATH%", "^|<>()", "space value", "", @"C:\path with space\", "--output", "json"];
        var result = await RunAzFixtureAsync(arguments);

        Assert.True(result.ExitCode == 0, $"stdout={result.StandardOutput}\nstderr={result.StandardError}");
        Assert.Equal(arguments, System.Text.Json.JsonSerializer.Deserialize<string[]>(result.StandardOutput));
    }

    [Fact]
    public void Runner_PreservesArgumentArraysForNativeExecutables()
    {
        string[] arguments = ["api", "--input", "space & value"];
        var request = new CommandBuilder(new TestEnvironment(isWindows: true)).Build("gh", arguments, "input");

        Assert.Equal("gh", request.FileName);
        Assert.Equal(arguments, request.Arguments);
        Assert.Null(request.WindowsCommandLine);
        Assert.Null(request.EnvironmentVariables);
        Assert.Equal("input", request.StandardInput);
    }

    private static async Task<ProcessResult> RunAzFixtureAsync(string[] arguments)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gh entra fixture {Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "args.ps1"), "[Console]::WriteLine((ConvertTo-Json -InputObject @($args) -Compress))");
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            // Like the Azure CLI launcher, forward %* to a native executable's argument parser.
            await File.WriteAllTextAsync(Path.Combine(tempDir, "az.cmd"), $"@echo off\r\n\"{powershell}\" -NoProfile -File \"%~dp0args.ps1\" %*\r\n");
            var request = new CommandBuilder(new TestEnvironment(isWindows: true)).Build("az", arguments);
            var environment = new Dictionary<string, string>(request.EnvironmentVariables!)
            {
                ["PATH"] = $"{tempDir};{Environment.GetEnvironmentVariable("PATH")}"
            };
            request = request with { EnvironmentVariables = environment };
            return await new SystemProcessRunner().RunAsync(request, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WindowsAzCmdWrapper_RejectsNewlineArguments()
    {
        var builder = new CommandBuilder(new TestEnvironment(isWindows: true));
        Assert.Throws<ArgumentException>(() => builder.Build("az", ["group", "show", "--name", "line1\nline2"]));
    }

    [Fact]
    public void WindowsAzCmdWrapper_RejectsDoubleQuoteArguments()
    {
        var builder = new CommandBuilder(new TestEnvironment(isWindows: true));
        Assert.Throws<ArgumentException>(() => builder.Build("az", ["group", "show", "--name", "rg\"q"]));
    }
}
