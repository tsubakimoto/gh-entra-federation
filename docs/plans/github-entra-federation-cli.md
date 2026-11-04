# GitHub CLI Extension: Microsoft Entra Federation Setup

- Status: Approved for implementation planning
- Date: 2026-11-04
- Target repository: `tsubakimoto/gh-entra-federation`
- Reference skill: [`tsubakimoto/skills/github-entra-federated-credentials`](https://github.com/tsubakimoto/skills/tree/main/github-entra-federated-credentials)

## Goal

Implement the setup workflow from the reference skill as a GitHub CLI Extension. The initial release configures Microsoft Entra workload identity federation for one GitHub repository Environment, optionally grants a role at an existing Azure Resource Group, stores the required identifiers as Environment secrets, and prints a usable workflow example.

## Approved scope and decisions

- Implement the initial Environment setup flow only. Listing, deleting, branch-based, and tag-based setup are out of scope.
- Use a C# self-contained executable GitHub CLI Extension; users should not need a separately installed .NET runtime.
- Invoke the user's installed and logged-in `az` and `gh` CLIs from C#.
- Require exactly one app-selection option: `--app-name` to create a new app registration, or `--app-id` to explicitly reuse an existing app. Never select an app by display-name search.
- Determine the OIDC subject from the repository's effective GitHub OIDC customization. Do not modify GitHub OIDC customization. Stop before writes if the subject is unsupported or cannot be determined.
- Before writes, show the target repository, Environment, tenant, subscription, app choice, subject, optional RBAC change, and possible secret replacement; require confirmation unless `--yes` is supplied.
- Stop at the first failed operation. Do not automatically delete or roll back created resources. Report created identifiers and recovery instructions, including how to resume with `--app-id`.
- Support GitHub.com and Azure public cloud. Do not silently use another GitHub host or Azure cloud.
- Build release assets for Windows and Linux, each on x64 and arm64.
- Do not create or commit a workflow file on the user's behalf. Print the workflow example after successful setup.
- Do not push changes, create a pull request, publish a release, or perform a real cloud setup as part of this plan.

## Command-line contract

```text
gh entra-federation setup --repo OWNER/REPO --environment ENVIRONMENT (--app-name NAME | --app-id CLIENT_ID) [--resource-group NAME] [--yes]
gh entra-federation --help
gh entra-federation --version
```

`--repo`, `--environment`, and exactly one of `--app-name` / `--app-id` are required for `setup`. Validate arguments and reject non-interactive execution without `--yes` before any write. Use the currently selected Azure tenant and subscription, and show both in the confirmation. A resource group is optional, must already exist, and receives Contributor only when explicitly supplied. Store `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` as GitHub Environment secrets, not variables.

## Architecture and implementation

1. **Resolve implementation constraints.** Recheck existing accepted ADRs. Before implementation, create ADRs for the confirmed long-term architecture decisions following `docs/adr/workflow.md`. Use a supported .NET LTS and stable CLI/test dependencies, pin dependency versions, and do not require NativeAOT or trimming.
2. **Create the extension scaffold.** Add a C# CLI project, test project, extension entry point and the minimum separate responsibilities for argument parsing, OIDC subject resolution, workflow rendering, setup orchestration, and external process execution.
3. **Implement a process seam.** Invoke external commands with argument arrays rather than shell-concatenated command strings. Preserve stdout, stderr, exit code, and cancellation behavior; handle Windows `az.cmd` invocation. Use `System.Text.Json` for JSON and reliably clean up temporary files.
4. **Preflight without writes.** Validate arguments, required tools and authentication; read the Azure account context; resolve the canonical GitHub repository name and numeric owner/repository IDs; inspect the repository's effective OIDC subject configuration; and validate any existing app or requested Resource Group. Stop if the effective subject cannot be determined. Do not infer the subject from repository creation date.
5. **Resolve OIDC subject correctly.** Support both legacy `repo:OWNER/REPO:environment:ENVIRONMENT` and immutable `repo:OWNER@OWNERID/REPO@REPOID:environment:ENVIRONMENT` forms according to the effective repository/org customization. Encode colons in Environment names as `%3A`. Read the repository OIDC customization endpoint and account for its `use_default`, `include_claim_keys`, `use_immutable_subject`, and `sub_claim_prefix` values. Stop before writes for unsupported custom claims, insufficient permissions, or an indeterminate effective subject. Do not add both subject forms unconditionally.
6. **Confirm all intended changes.** Present the repository, Environment, Azure tenant/subscription, app operation, exact OIDC subject, optional Contributor assignment, and possible secret overwrites. Continue only after confirmation, or immediately with `--yes`.
7. **Apply Entra changes.** Create the app registration for `--app-name`, or reuse only the explicitly supplied `--app-id`; create/reuse its service principal as required. Create a federated identity credential with issuer `https://token.actions.githubusercontent.com` and audience `api://AzureADTokenExchange`. Use a stable, sanitized credential name derived from repository and Environment identity; detect normalization collisions. Skip an identical existing credential, but stop rather than overwrite a conflicting credential.
8. **Apply optional RBAC.** For a requested existing Resource Group, grant Contributor at that Resource Group's subscription scope to the exact service principal. Skip an exact existing role/principal/scope assignment; do not broaden scope.
9. **Configure GitHub.** Preserve an existing Environment and its protection settings; create it only when absent. Set the three Azure values as Environment-scoped secrets. Stop on the first failure.
10. **Report and recover.** On failure, include the failed operation, useful CLI error detail, identifiers of resources already created, and a safe resume path using `--app-id`. Never silently retry permission or input errors, and never perform automatic rollback. Use bounded retries only for clearly identified propagation delays.
11. **Print workflow guidance.** On success, print the resulting resource identifiers and a valid `azure/login` GitHub Actions example using the configured Environment and `id-token: write`. Do not write workflow files.

## Planned repository changes

- `src/gh-entra-federation/`: extension executable, command handling, setup orchestration, process runner, subject resolution, and workflow output.
- `tests/gh-entra-federation.Tests/`: unit tests using a fake process runner; tests must not contact Azure or GitHub.
- `.github/workflows/ci.yml`: build and test workflow.
- `.github/workflows/release.yml`: tag-triggered publication workflow for four self-contained assets, without publishing a release during implementation.
- `README.md`: replace template text with extension installation, authentication and permissions, usage, recovery, supported platforms, and validation boundaries.
- `docs/adr/`: ADRs required by the repository's architecture decision workflow before implementation.

Use GitHub CLI Extension asset naming conventions, for example `gh-entra-federation-windows-amd64.exe` and `gh-entra-federation-linux-arm64`.

## Verification plan

### Unit and behavior tests

Use fake process responses and assert both outcomes and exact external command arguments. Cover:

- Missing, conflicting, and invalid arguments; help/version; missing CLIs; unauthenticated `az`/`gh`; non-interactive execution without `--yes`; cancellation; and confirmation rejection all produce no writes.
- Legacy and immutable OIDC subjects, Environment colon encoding, customization read failures, unsupported custom claims, and indeterminate effective configuration all resolve correctly or stop before writes.
- Repository/API path encoding, JSON/YAML escaping, stable credential-name normalization, and collisions.
- New and explicitly reused app registrations; service-principal creation/reuse; identical FIC skip; conflicting FIC stop; exact RBAC skip; optional Resource Group behavior; existing Environment protection preservation; and Environment secret updates.
- Every write failure stops subsequent operations and reports already-created identifiers and recovery instructions.
- Workflow output includes Environment-scoped secrets and `id-token: write` without modifying GitHub OIDC settings or writing files.

### Build, distribution, and smoke checks

- Run the focused test suite and build.
- Publish self-contained executable assets for Windows/Linux x64/arm64.
- Run `--help` and `--version` on the current host; run architecture-native smoke checks only where the host can execute those assets. Report cross-publish separately from execution.
- Validate the generated workflow YAML and inspect the release/CI workflows.
- Use CI for build/tests and platform-native smoke checks where supported.

Tests must use fake process execution and make no cloud changes. Real Entra/GitHub end-to-end testing is outside this implementation plan and requires a separately approved target and authorization.

## Completion criteria

- The documented command contract and preflight-before-write boundary are implemented.
- Effective OIDC subject selection handles legacy and immutable formats without changing GitHub OIDC settings or guessing.
- Setup is explicit, resumable, and fail-fast; existing Environment protections and exact-scope RBAC are preserved.
- Unit tests cover the failure, conflict, idempotency, encoding, and output cases above without contacting cloud services.
- Windows/Linux x64/arm64 self-contained assets can be built, and CI/release workflows and README match the implementation.
- No push, pull request, release publication, or real cloud setup is performed without separate authorization.

## References

- [GitHub Actions OIDC reference](https://docs.github.com/en/actions/reference/security/oidc)
- [GitHub Actions OIDC REST API](https://docs.github.com/en/rest/actions/oidc)
- [Creating GitHub CLI Extensions](https://docs.github.com/en/github-cli/github-cli/creating-github-cli-extensions)
