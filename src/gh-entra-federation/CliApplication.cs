using System.Reflection;

namespace GhEntraFederation;

public sealed class CliApplication(IProcessRunner processRunner, IConsole console, IRuntimeEnvironment runtimeEnvironment)
{
    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        try
        {
            if (args.Count == 0 || args[0] is "--help" or "-h")
            {
                WriteHelp();
                return 0;
            }

            if (args[0] is "--version" or "-v")
            {
                console.WriteLine(GetVersion());
                return 0;
            }

            if (args[0] == "setup")
            {
                var options = SetupOptionsParser.Parse(args);
                var commandBuilder = new CommandBuilder(runtimeEnvironment);
                var cli = new CliFacade(processRunner, commandBuilder);
                var orchestrator = new SetupOrchestrator(cli, console);
                return await orchestrator.RunAsync(options, cancellationToken);
            }

            throw new ArgumentException($"Unknown command: {args[0]}");
        }
        catch (OperationCanceledException)
        {
            console.WriteErrorLine("Operation canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            console.WriteErrorLine(ex.Message);
            return 1;
        }
    }

    private void WriteHelp()
    {
        console.WriteLine("gh entra-federation setup --repo OWNER/REPO --environment ENVIRONMENT (--app-name NAME | --app-id CLIENT_ID) [--resource-group NAME] [--yes]");
        console.WriteLine("gh entra-federation --help");
        console.WriteLine("gh entra-federation --version");
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return NormalizeVersion(informational);
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.1.0";
    }

    internal static string NormalizeVersion(string informationalVersion)
    {
        var plusIndex = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
    }
}
