# gh-entra-federation

GitHub CLI extension for configuring Microsoft Entra workload identity federation for one GitHub repository Environment. Uses your installed, logged-in `gh` and `az` CLIs; no separate .NET runtime is required for released executables.

## Install and authenticate

After a release with executable assets is published:

```powershell
gh extension install tsubakimoto/gh-entra-federation
gh auth login --hostname github.com
az login
az account set --subscription YOUR_SUBSCRIPTION_ID
gh entra-federation --help
```

A source-only repository without a published binary release is not yet installable as a precompiled extension. For local development, use the build commands below and run the executable directly.

Supported targets: Windows x64/arm64 and Linux x64/arm64 (glibc, not Alpine/musl). GitHub.com and Azure public cloud only. The selected Azure account supplies the tenant and subscription; the extension does not switch either. macOS, GitHub Enterprise Server, sovereign Azure clouds, and branch/tag federation are not supported.

### Permissions

- GitHub: repository administration access to read OIDC customization and read/create the Environment, plus permission to write Environment secrets. Organization OIDC inheritance may require access to the organization customization endpoint. For classic tokens, private repositories generally require `repo`; fine-grained tokens require the appropriate repository Actions, Environments, and Secrets permissions. Repository/organization policies and SSO authorization still apply.
- Microsoft Entra: permission to create an app registration when using `--app-name`, or permission to manage the explicitly selected existing app when using `--app-id`, including its federated credentials and service principal. Tenant policies can restrict application registration and service-principal creation.
- Azure: read access to the selected subscription and requested existing Resource Group. `--resource-group` additionally requires role-assignment write permission at that Resource Group (for example, Role Based Access Control Administrator or Owner). Contributor alone cannot grant roles.

The extension does not request broader permissions or log in for you. Preflight reads are not a guarantee that subsequent writes are authorized.

## Usage

```powershell
gh entra-federation setup --repo OWNER/REPO --environment production --app-name github-production

gh entra-federation setup --repo OWNER/REPO --environment production --app-id CLIENT_ID --resource-group existing-rg --yes

gh entra-federation --version
```

Required: `--repo`, `--environment`, and **exactly one** of `--app-name` or `--app-id`. Display-name lookup is never used to select an app. `--app-name` always creates a new registration; use `--app-id` to resume or reuse. Option values cannot start with `--`; app-selection and Resource Group values cannot contain line breaks or double quotes. These restrictions prevent option injection and unsafe Windows `az.cmd` invocation. On Windows, the extension invokes `az.cmd` through `cmd.exe` using child-process environment variables to preserve argument values without adding literal quotes. Federated credential JSON is passed using a temporary file instead of inline JSON; the file is deleted after the command, including on failure or cancellation.

Before writes, the extension validates CLI/authentication context, canonical repository identity, effective OIDC subject configuration, the selected existing app, and any requested Resource Group. It shows the repository, Environment, tenant/subscription, app operation, exact subject, optional RBAC assignment, and possible secret replacements. Confirm interactively, or provide `--yes` for non-interactive execution. `--yes` skips confirmation, not validation.

Setup creates/reuses the app's service principal and an Environment-specific federated identity credential. It supports legacy and immutable GitHub subjects, encodes Environment colons as `%3A`, and stops for unsupported or indeterminate customization rather than guessing or changing GitHub OIDC settings. Identical credentials are skipped; conflicting names/credentials are not overwritten.

If `--resource-group` is specified, it must already exist in the selected subscription. Contributor is granted only to the exact service principal at that Resource Group's scope; an identical assignment is skipped. No Resource Group is created and no subscription-wide role is granted.

Existing Environment protection settings are preserved. The following **Environment secrets** are set or replaced:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

On success, resource identifiers and a YAML workflow example using `azure/login`, Environment-scoped secrets, and `id-token: write` are printed. Review and save the example yourself; the extension never creates or commits a workflow file.

## Failure and recovery

Setup stops at the first failure. Created resources and already-updated secrets remain; there is no automatic rollback. Error output identifies the failed operation, available resource identifiers, and a resume command using `--app-id`.

Fix the reported authentication, permission, propagation, or conflict problem, then resume with the reported client ID and the same repository/Environment/optional Resource Group. Do **not** rerun `--app-name` to resume: it creates another registration. Inspect conflicting credentials manually instead of deleting or replacing them blindly. After a partial secret update, a successful rerun sets all three values consistently.

## Development and verification

Build with the .NET 10 SDK selected by `global.json`:

```powershell
dotnet test .\tests\gh-entra-federation.Tests\gh-entra-federation.Tests.csproj --configuration Release
dotnet publish .\src\gh-entra-federation\gh-entra-federation.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false --output .\artifacts\win-x64
.\artifacts\win-x64\gh-entra-federation.exe --help
.\artifacts\win-x64\gh-entra-federation.exe --version
```

For cross-publishing, replace `win-x64` with `win-arm64`, `linux-x64`, or `linux-arm64` and use a separate output directory. Publishing for another architecture does not prove that its executable runs; smoke checks must use a compatible host.

CI runs fake-runner tests, self-contained single-file publishing, and native help/version checks on all four target runners. Tests do not contact Azure or GitHub. A stable `vMAJOR.MINOR.PATCH` tag triggers the release workflow, which gates publication on those checks and attaches:

| Target | Release asset |
| --- | --- |
| Windows x64 | `gh-entra-federation-windows-amd64.exe` |
| Windows arm64 | `gh-entra-federation-windows-arm64.exe` |
| Linux x64 | `gh-entra-federation-linux-amd64` |
| Linux arm64 | `gh-entra-federation-linux-arm64` |

No real Entra/GitHub end-to-end setup is part of automated verification. It requires separately authorized targets and cloud-write permission. Release publication also requires a separately authorized tag push; implementing these workflows does not publish a release.

## Design and references

- [Approved implementation plan](docs/plans/github-entra-federation-cli.md)
- [Self-contained C# decision](docs/adr/0001-use-self-contained-csharp-extension.md)
- [Logged-in CLI contexts decision](docs/adr/0002-reuse-logged-in-cli-contexts.md)
- [Creating GitHub CLI extensions](https://docs.github.com/en/github-cli/github-cli/creating-github-cli-extensions)
- [GitHub OIDC reference](https://docs.github.com/en/actions/reference/security/oidc)
- [OIDC REST API](https://docs.github.com/en/rest/actions/oidc)
