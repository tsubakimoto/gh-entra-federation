namespace GhEntraFederation;

public sealed class SystemConsole : IConsole
{
    public bool IsInteractive => !Console.IsInputRedirected;

    public Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"{message} [y/N]");
        var line = Console.ReadLine();
        return Task.FromResult(string.Equals(line, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(line, "yes", StringComparison.OrdinalIgnoreCase));
    }

    public void WriteErrorLine(string message)
    {
        Console.Error.WriteLine(message);
    }

    public void WriteLine(string message)
    {
        Console.WriteLine(message);
    }
}
