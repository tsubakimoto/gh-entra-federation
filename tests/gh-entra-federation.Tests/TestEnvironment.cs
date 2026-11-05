using GhEntraFederation;

namespace gh_entra_federation.Tests;

internal sealed class TestEnvironment(bool isWindows) : IRuntimeEnvironment
{
    public bool IsWindows { get; } = isWindows;
}
