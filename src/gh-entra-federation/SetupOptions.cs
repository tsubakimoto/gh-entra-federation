namespace GhEntraFederation;

public sealed record SetupOptions(
    string Repo,
    string Environment,
    string? AppName,
    string? AppId,
    string? ResourceGroup,
    bool Yes);

public static class SetupOptionsParser
{
    public static SetupOptions Parse(IReadOnlyList<string> args)
    {
        string? repo = null;
        string? environment = null;
        string? appName = null;
        string? appId = null;
        string? resourceGroup = null;
        var yes = false;

        for (var i = 1; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--repo":
                    repo = ReadValue(args, ref i, "--repo");
                    break;
                case "--environment":
                    environment = ReadValue(args, ref i, "--environment");
                    break;
                case "--app-name":
                    appName = ReadValue(args, ref i, "--app-name");
                    break;
                case "--app-id":
                    appId = ReadValue(args, ref i, "--app-id");
                    break;
                case "--resource-group":
                    resourceGroup = ReadValue(args, ref i, "--resource-group");
                    break;
                case "--yes":
                    yes = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(repo))
        {
            throw new ArgumentException("--repo is required.");
        }

        if (string.IsNullOrWhiteSpace(environment))
        {
            throw new ArgumentException("--environment is required.");
        }

        var appNameProvided = !string.IsNullOrWhiteSpace(appName);
        var appIdProvided = !string.IsNullOrWhiteSpace(appId);
        if (appNameProvided == appIdProvided)
        {
            throw new ArgumentException("Exactly one of --app-name or --app-id is required.");
        }

        if (!repo.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("--repo must be in OWNER/REPO format.");
        }

        ValidateNoNewLine("--repo", repo);
        ValidateNoNewLine("--environment", environment);
        if (appName is not null)
        {
            ValidateNoNewLine("--app-name", appName);
            ValidateNoDoubleQuote("--app-name", appName);
        }

        if (appId is not null)
        {
            ValidateNoNewLine("--app-id", appId);
            ValidateNoDoubleQuote("--app-id", appId);
        }

        if (resourceGroup is not null)
        {
            ValidateNoNewLine("--resource-group", resourceGroup);
            ValidateNoDoubleQuote("--resource-group", resourceGroup);
        }

        return new SetupOptions(repo, environment, appName, appId, resourceGroup, yes);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string name)
    {
        var next = index + 1;
        if (next >= args.Count || args[next].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{name} requires a value.");
        }

        index = next;
        return args[next];
    }

    private static void ValidateNoNewLine(string name, string value)
    {
        if (value.Contains('\r') || value.Contains('\n'))
        {
            throw new ArgumentException($"{name} must not contain newline characters.");
        }
    }

    private static void ValidateNoDoubleQuote(string name, string value)
    {
        if (value.Contains('"'))
        {
            throw new ArgumentException($"{name} must not contain double quote characters.");
        }
    }
}
