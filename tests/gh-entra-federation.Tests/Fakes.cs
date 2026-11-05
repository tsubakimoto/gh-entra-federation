using System.Text;

using GhEntraFederation;

namespace gh_entra_federation.Tests;

internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Queue<(Func<ProcessRequest, bool> Match, ProcessResult Result)> _responses = new();

    public List<ProcessRequest> Calls { get; } = new();

    public void Enqueue(Func<ProcessRequest, bool> matcher, ProcessResult result)
    {
        _responses.Enqueue((matcher, result));
    }

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        Calls.Add(request);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No fake response for command: {request.FileName} {string.Join(' ', request.Arguments)}");
        }

        var entry = _responses.Dequeue();
        if (!entry.Match(request))
        {
            throw new InvalidOperationException($"Unexpected command: {request.FileName} {string.Join(' ', request.Arguments)}");
        }

        return Task.FromResult(entry.Result);
    }
}

internal sealed class CancelingProcessRunner : IProcessRunner
{
    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        throw new OperationCanceledException(cancellationToken);
    }
}

internal sealed class FakeConsole(bool isInteractive, bool confirmResult = true) : IConsole
{
    public StringBuilder Out { get; } = new();
    public StringBuilder Error { get; } = new();

    public bool IsInteractive { get; } = isInteractive;

    public bool ConfirmResult { get; set; } = confirmResult;

    public void WriteLine(string message)
    {
        Out.AppendLine(message);
    }

    public void WriteErrorLine(string message)
    {
        Error.AppendLine(message);
    }

    public Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken)
    {
        Out.AppendLine(message);
        return Task.FromResult(ConfirmResult);
    }
}

internal static class Matchers
{
    public static Func<ProcessRequest, bool> Command(string fileName, params string[] args)
    {
        return request => string.Equals(request.FileName, fileName, StringComparison.Ordinal)
            && request.Arguments.SequenceEqual(args);
    }
}
