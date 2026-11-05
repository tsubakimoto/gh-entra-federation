using System.Runtime.InteropServices;

namespace GhEntraFederation;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

public readonly record struct ProcessRequest(string FileName, IReadOnlyList<string> Arguments, string? StandardInput = null)
{
    public string? WindowsCommandLine { get; init; }

    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
}

public readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IConsole
{
    bool IsInteractive { get; }

    void WriteLine(string message);

    void WriteErrorLine(string message);

    Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken);
}

public interface IRuntimeEnvironment
{
    bool IsWindows { get; }
}

public sealed class RuntimeEnvironment : IRuntimeEnvironment
{
    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}
