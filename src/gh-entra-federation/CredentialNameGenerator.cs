using System.Security.Cryptography;
using System.Text;

namespace GhEntraFederation;

public static class CredentialNameGenerator
{
    public static string Generate(string owner, string repo, string environment)
    {
        var baseName = $"gh-entra-{owner}-{repo}-{environment}".ToLowerInvariant();
        var normalized = Normalize(baseName);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{owner}/{repo}:{environment}"))).ToLowerInvariant()[..8];
        var name = $"{normalized}-{hash}";
        return name.Length <= 120 ? name : name[..120];
    }

    private static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        var previousDash = false;
        foreach (var ch in value)
        {
            var next = char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-';
            if (next == '-')
            {
                if (previousDash)
                {
                    continue;
                }

                previousDash = true;
                sb.Append(next);
                continue;
            }

            previousDash = false;
            sb.Append(next);
        }

        var normalized = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(normalized) ? "gh-entra-fic" : normalized;
    }
}
