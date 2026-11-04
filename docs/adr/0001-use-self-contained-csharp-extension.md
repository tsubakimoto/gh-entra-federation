---
status: accepted
date: 2026-11-04
deciders: tsubakimoto
---

# ADR-0001: Use a Self-Contained C# Extension

## Context and Problem Statement

The approved implementation plan calls for a GitHub CLI Extension with executable assets for Windows and Linux on x64 and arm64. The implementation language and runtime distribution model need to be recorded as long-term project decisions.

## Decision Drivers

- Provide the planned Windows and Linux x64 and arm64 assets.
- Distribute a self-contained executable that does not require users to install a .NET runtime separately.

## Considered Options

- Go precompiled (official scaffold)
- C# self-contained
- PowerShell script

## Decision Outcome

選択肢: **"C# self-contained"**

理由: 承認済みPlanで選択された実装方式であり、利用者側に .NET Runtime の別途インストールを要求しない self-contained executable として配布する。

### Consequences

- 良い点: 利用者は別途 .NET Runtime をインストールせずに実行できる。
- 悪い点: Windows/Linux の x64/arm64 ごとに実行ファイルを配布する必要がある。

## Validation

Plan に定めた Windows/Linux x64/arm64 の self-contained publish と、実行可能な現在の host における `--help` / `--version` smoke check で確認する。

## Pros and Cons of the Options

### Go precompiled (official scaffold)

- 良い点: GitHub CLI Extension の公式 scaffold を利用できる。
- 中立: precompiled executable を配布する。
- 悪い点: Plan で選択された実装方式ではない。

### C# self-contained

- 良い点: 利用者側に .NET Runtime を別途要求しない executable を配布できる。
- 中立: Windows/Linux x64/arm64 ごとの asset を用意する。
- 悪い点: 選択された配布方式では、.NET Runtime を実行ファイルに含める。

### PowerShell script

- 良い点: script として実装できる。
- 中立: `az` と `gh` CLI を呼び出す。
- 悪い点: 実行環境に PowerShell が必要になる。

## More Information

承認済み実装計画: [GitHub CLI Extension: Microsoft Entra Federation Setup](../plans/github-entra-federation-cli.md)
