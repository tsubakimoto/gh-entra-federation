using System.Text.Json.Serialization;

namespace GhEntraFederation;

public sealed record AzureAccount(
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("id")] string SubscriptionId,
    [property: JsonPropertyName("environmentName")] string EnvironmentName);

public sealed record GitHubOwner(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type);

public sealed record GitHubRepository(
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("owner")] GitHubOwner Owner);

public sealed record RepoOidcTemplate(
    [property: JsonPropertyName("use_default")] bool UseDefault,
    [property: JsonPropertyName("include_claim_keys")] string[]? IncludeClaimKeys,
    [property: JsonPropertyName("use_immutable_subject")] bool? UseImmutableSubject,
    [property: JsonPropertyName("sub_claim_prefix")] string? SubClaimPrefix);

public sealed record OrgOidcTemplate(
    [property: JsonPropertyName("include_claim_keys")] string[] IncludeClaimKeys,
    [property: JsonPropertyName("use_immutable_subject")] bool? UseImmutableSubject);

public sealed record AppInfo(
    [property: JsonPropertyName("appId")] string AppId,
    [property: JsonPropertyName("id")] string ObjectId);

public sealed record ServicePrincipalInfo(
    [property: JsonPropertyName("id")] string Id);

public sealed record FederatedCredentialInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("audiences")] string[] Audiences);

public sealed record RoleAssignmentInfo(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("principalId")] string PrincipalId,
    [property: JsonPropertyName("roleDefinitionName")] string RoleDefinitionName);
