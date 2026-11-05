using GhEntraFederation;

namespace gh_entra_federation.Tests;

public sealed class SetupOptionsParserTests
{
    [Fact]
    public void Parser_RequiresExactlyOneAppOption()
    {
        Assert.Throws<ArgumentException>(() => SetupOptionsParser.Parse(["setup", "--repo", "o/r", "--environment", "prod"]));
        Assert.Throws<ArgumentException>(() => SetupOptionsParser.Parse(["setup", "--repo", "o/r", "--environment", "prod", "--app-id", "a", "--app-name", "n"]));
    }

    [Fact]
    public void Parser_ParsesRequiredAndOptionalFlags()
    {
        var parsed = SetupOptionsParser.Parse(["setup", "--repo", "o/r", "--environment", "prod", "--app-name", "sample", "--resource-group", "rg1", "--yes"]);

        Assert.Equal("o/r", parsed.Repo);
        Assert.Equal("prod", parsed.Environment);
        Assert.Equal("sample", parsed.AppName);
        Assert.Equal("rg1", parsed.ResourceGroup);
        Assert.True(parsed.Yes);
    }

    [Fact]
    public void Parser_RejectsInjectedOptionAsValue()
    {
        Assert.Throws<ArgumentException>(() => SetupOptionsParser.Parse(["setup", "--repo", "o/r", "--environment", "prod", "--app-id", "id1", "--resource-group", "--subscription"]));
    }

    [Fact]
    public void Parser_RejectsDangerousCharactersForAzBoundOptions()
    {
        Assert.Throws<ArgumentException>(() => SetupOptionsParser.Parse(["setup", "--repo", "o/r", "--environment", "prod", "--app-name", "bad\"name"]));
        Assert.Throws<ArgumentException>(() => SetupOptionsParser.Parse(["setup", "--repo", "o/r", "--environment", "prod", "--app-id", "bad\nid"]));
    }
}
