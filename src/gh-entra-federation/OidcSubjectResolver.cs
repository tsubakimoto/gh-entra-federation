namespace GhEntraFederation;

public sealed record OidcResolution(string Subject, bool UseImmutableSubject);

public static class OidcSubjectResolver
{
    private static readonly string[] RequiredClaims = ["repo", "context"];

    public static OidcResolution Resolve(
        GitHubRepository repository,
        string environmentName,
        RepoOidcTemplate repositoryTemplate,
        OrgOidcTemplate? organizationTemplate)
    {
        var template = ResolveEffectiveTemplate(repository, repositoryTemplate, organizationTemplate);
        var subject = BuildSubject(repository, environmentName, template.UseImmutableSubject);
        return new OidcResolution(subject, template.UseImmutableSubject);
    }

    private static (bool UseImmutableSubject, string Prefix) ResolveEffectiveTemplate(
        GitHubRepository repository,
        RepoOidcTemplate repositoryTemplate,
        OrgOidcTemplate? organizationTemplate)
    {
        if (repositoryTemplate.UseDefault)
        {
            if (repositoryTemplate.UseImmutableSubject is null)
            {
                throw new InvalidOperationException("Unsupported OIDC subject customization: default subject immutability cannot be determined.");
            }

            return (repositoryTemplate.UseImmutableSubject.Value, "repo");
        }

        if (repositoryTemplate.IncludeClaimKeys is { Length: > 0 })
        {
            ValidateClaimKeys(repositoryTemplate.IncludeClaimKeys);
            ValidatePrefix(repositoryTemplate.SubClaimPrefix);
            if (repositoryTemplate.UseImmutableSubject is null)
            {
                throw new InvalidOperationException("Unsupported OIDC subject customization: use_immutable_subject is not explicit.");
            }

            return (repositoryTemplate.UseImmutableSubject.Value, repositoryTemplate.SubClaimPrefix ?? "repo");
        }

        if (string.Equals(repository.Owner.Type, "Organization", StringComparison.OrdinalIgnoreCase))
        {
            if (organizationTemplate is null)
            {
                throw new InvalidOperationException("Repository opted into organization OIDC template but organization template could not be resolved.");
            }

            ValidateClaimKeys(organizationTemplate.IncludeClaimKeys);
            if (organizationTemplate.UseImmutableSubject is null)
            {
                throw new InvalidOperationException("Unsupported OIDC subject customization: organization template does not explicitly define use_immutable_subject.");
            }

            return (organizationTemplate.UseImmutableSubject.Value, "repo");
        }

        throw new InvalidOperationException("Repository opted into inherited template but no organization template is available.");
    }

    private static void ValidatePrefix(string? prefix)
    {
        if (prefix is null)
        {
            return;
        }

        if (!string.Equals(prefix, "repo", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsupported OIDC subject customization: sub_claim_prefix must be 'repo'.");
        }
    }

    private static void ValidateClaimKeys(string[]? includeClaimKeys)
    {
        if (includeClaimKeys is null)
        {
            throw new InvalidOperationException("Unsupported OIDC subject customization: include_claim_keys missing.");
        }

        var keys = includeClaimKeys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var required = RequiredClaims.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!keys.SequenceEqual(required, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Unsupported OIDC subject customization: only ['repo','context'] is supported.");
        }
    }

    private static string BuildSubject(GitHubRepository repository, string environmentName, bool immutable)
    {
        var encodedEnvironment = environmentName.Replace(":", "%3A", StringComparison.Ordinal);
        return immutable
            ? $"repo:{repository.Owner.Login}@{repository.Owner.Id}/{repository.Name}@{repository.Id}:environment:{encodedEnvironment}"
            : $"repo:{repository.Owner.Login}/{repository.Name}:environment:{encodedEnvironment}";
    }
}
