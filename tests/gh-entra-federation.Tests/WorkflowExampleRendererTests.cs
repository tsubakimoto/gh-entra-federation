using GhEntraFederation;

namespace gh_entra_federation.Tests;

public sealed class WorkflowExampleRendererTests
{
    [Fact]
    public void Render_IsDeterministic()
    {
        var first = WorkflowExampleRenderer.Render("prod");
        var second = WorkflowExampleRenderer.Render("prod");

        Assert.Equal(first, second);
        Assert.Contains("id-token: write", first);
        Assert.Contains("environment: prod", first);
    }
}
