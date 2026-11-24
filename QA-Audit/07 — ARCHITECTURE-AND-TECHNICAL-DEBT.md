# Architecture and technical debt

## Architecture map (reconnaissance)

| Layer | Projects / responsibilities |
|---|---|
| Desktop shell | `OpenClaw.Tray.WinUI` (WinUI executable), `OpenClawTray.FunctionalUI` |
| Core domain | `OpenClaw.Shared` (orchestration, approvals, tools, voice, study, creative, coding) |
| Connectivity | `OpenClaw.Connection` (Gateway, pairing, WSL/native runtime, diagnostics) |
| Conversational domain | `OpenClaw.Chat` |
| Setup | `OpenClaw.SetupEngine`, `OpenClaw.SetupEngine.UI` |
| Executable support | `OpenClaw.FileWorker`, `OpenClaw.WinNode.Cli`, `OpenClaw.Cli`, `OpenClaw.GatewayCleanupHost` |

There are 11 discovered production projects and multiple dedicated test projects, including fixture, UI, connection, CLI, packaging, and functional UI suites.

## Confirmed debt

The changed approval contract lacks a compile-time safety net that would have stopped the incompatible interface edit at review time. The tests identified for `SparkApprovalManager` cover basic request/approve/reject/cancel behavior but do not include the new event contract.

## Audit constraint

The worktree contains extensive pre-existing modifications and an untracked nested `Spark-main/` directory. The audit made no source changes and does not attribute those changes to a particular author.

Repository-wide documentation validation is currently red because existing root and nested documentation contains forbidden em dashes and Mermaid diagrams. The audit reports themselves were not listed as violations.
