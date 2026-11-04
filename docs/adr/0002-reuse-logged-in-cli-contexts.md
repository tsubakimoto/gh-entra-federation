---
status: accepted
date: 2026-11-04
deciders: tsubakimoto
---

# ADR-0002: Reuse Logged-In CLI Contexts

## Context and Problem Statement

The approved implementation plan configures Microsoft Entra and GitHub repository settings from a C# GitHub CLI Extension. The authentication and service-operation mechanism needs to be recorded as a long-term project decision.

## Decision Drivers

- Use the user's installed and logged-in `az` and `gh` CLI contexts.
- Keep the requested setup workflow as a CLI extension that invokes those tools.

## Considered Options

- Invoke the installed `az` and `gh` CLIs.
- Use SDKs or direct REST calls.
- Generate and apply IaC.

## Decision Outcome

選択肢: **"Invoke the installed `az` and `gh` CLIs."**

理由: 承認済みPlanでC#からログイン済みの `az` と `gh` を呼び出す方式が選択されている。

### Consequences

- 良い点: ユーザーは既存の `az` / `gh` CLI認証コンテキストを利用できる。
- 悪い点: 対象環境に `az` と `gh` がインストールされ、ログイン済みである必要がある。

## Validation

fake process runner を用いるテストでコマンド引数と失敗時の停止動作を確認する。実クラウドの E2E は承認済みPlanの範囲外とする。

## Pros and Cons of the Options

### Invoke the installed `az` and `gh` CLIs

- 良い点: ユーザーの既存CLIログインを利用してAzureおよびGitHub操作を行える。
- 中立: C# から外部プロセスを呼び出す。
- 悪い点: `az` と `gh` がインストール済みでログイン状態であることが必要になる。

### Use SDKs or direct REST calls

- 良い点: SDKまたはHTTP APIを直接利用して操作できる。
- 中立: C#アプリケーション内でサービス操作を行う。
- 悪い点: Plan で選択されたログイン済みCLI呼び出し方式とは異なり、認証の実装が必要になる。

### Generate and apply IaC

- 良い点: 設定をIaCとして表現できる。
- 中立: CLIによる直接セットアップとは異なる実行モデルになる。
- 悪い点: Plan で選択されたCLIセットアップの実行モデルを変更する。

## More Information

承認済み実装計画: [GitHub CLI Extension: Microsoft Entra Federation Setup](../plans/github-entra-federation-cli.md)
