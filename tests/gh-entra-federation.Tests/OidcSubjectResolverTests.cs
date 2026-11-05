using GhEntraFederation;

namespace gh_entra_federation.Tests;

public sealed class OidcSubjectResolverTests
{
    [Fact]
    public void Resolve_Fails_WhenRepoTemplateMissingUseImmutable()
    {
        var repo = CreateRepository("User");
        var template = new RepoOidcTemplate(false, ["repo", "context"], null, "repo");

        var ex = Assert.Throws<InvalidOperationException>(() => OidcSubjectResolver.Resolve(repo, "prod", template, null));
        Assert.Contains("use_immutable_subject", ex.Message);
    }

    [Fact]
    public void Resolve_Fails_WhenUseDefaultTrueAndUserRepoMissingFields()
    {
        var repo = CreateRepository("User");
        var template = new RepoOidcTemplate(true, null, null, null);

        var ex = Assert.Throws<InvalidOperationException>(() => OidcSubjectResolver.Resolve(repo, "prod", template, null));
        Assert.Contains("immutability", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_Fails_WhenOrgTemplateMissingUseImmutable()
    {
        var repo = CreateRepository("Organization");
        var repoTemplate = new RepoOidcTemplate(false, null, null, null);
        var orgTemplate = new OrgOidcTemplate(["repo", "context"], null);

        var ex = Assert.Throws<InvalidOperationException>(() => OidcSubjectResolver.Resolve(repo, "prod", repoTemplate, orgTemplate));
        Assert.Contains("organization template", ex.Message);
    }

    private static GitHubRepository CreateRepository(string ownerType)
    {
        return new GitHubRepository("octo/repo", "repo", 22, new GitHubOwner("octo", 11, ownerType));
    }
}
