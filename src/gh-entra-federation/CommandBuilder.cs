namespace GhEntraFederation;

public sealed class CommandBuilder(IRuntimeEnvironment runtimeEnvironment)
{
    public ProcessRequest Build(string fileName, IReadOnlyList<string> arguments, string? standardInput = null)
    {
        if (runtimeEnvironment.IsWindows && string.Equals(fileName, "az", StringComparison.OrdinalIgnoreCase))
        {
            var environment = new Dictionary<string, string>();
            var encoded = new List<string> { "az.cmd" };
            for (var index = 0; index < arguments.Count; index++)
            {
                var value = arguments[index];
                ValidateAzArgument(value);
                if (value.Length == 0)
                {
                    encoded.Add("\"\"");
                    continue;
                }

                var variable = $"GH_ENTRA_AZ_ARG_{index}";
                // Expand each value once inside quotes; literal %, ! and shell metacharacters stay data.
                environment[variable] = value;
                var trailingBackslashes = value.Length - value.TrimEnd('\\').Length;
                encoded.Add($"\"%{variable}%{new string('\\', trailingBackslashes)}\"");
            }

            var command = string.Join(' ', encoded);
            return new ProcessRequest("cmd.exe", [], standardInput)
            {
                // cmd.exe does not understand the backslash-escaped quotes emitted by ArgumentList.
                WindowsCommandLine = $"/d /v:off /s /c \"{command}\"",
                EnvironmentVariables = environment
            };
        }

        return new ProcessRequest(fileName, arguments, standardInput);
    }

    private static void ValidateAzArgument(string value)
    {
        if (value.Contains('\r') || value.Contains('\n'))
        {
            throw new ArgumentException("Newline characters are not allowed in command arguments.");
        }

        if (value.Contains('"'))
        {
            throw new ArgumentException("Double quote characters are not allowed in command arguments for az on Windows.");
        }
    }
}
