# Spark - Baseline Quality Assurance Audit Report

## 1. Executive Summary

This report establishes the baseline engineering and QA state of Spark following the integration of the upstream OpenClaw Gateway into the Windows Hub repository.

- **Solution**: `openclaw-windows-node.slnx` (22 projects: 11 source projects, 11 test projects).
- **Core Upstream Repository**: `C:\Users\PC\Documents\Spark\openclaw-main` (TypeScript, Node.js v26.13.0, pnpm 12.18.0).
- **Windows Foundation Repository**: `C:\Users\PC\Documents\Spark\openclaw-windows-node-main` (C# .NET 10.0.404, WinUI 3, Windows App SDK 2.6.5).
- **Compilation Status**: Clean build of all 22 projects succeeded with 0 warnings and 0 errors.
- **Documentation Status**: `scripts/validate-docs.ps1` validated 88 markdown files with 0 errors.

---

## 2. Inventory of Source & Test Projects

### Application Projects (11)
1. `src/OpenClaw.Shared`: Core domain models, crypto (Ed25519), settings, audio STT/TTS abstractions.
2. `src/OpenClaw.Connection`: WebSocket client, Gateway lifecycle management, pairing, authentication.
3. `src/OpenClaw.Chat`: Chat message schemas, session coordination, timeline models.
4. `src/OpenClawTray.FunctionalUI`: UI state models, reactive property definitions.
5. `src/OpenClaw.SetupEngine`: First-run onboarding wizard engine and verifiers.
6. `src/OpenClaw.SetupEngine.UI`: WinUI 3 setup wizard dialog views.
7. `src/OpenClaw.GatewayCleanupHost`: .NET Framework 4.8 out-of-process uninstallation cleanup helper.
8. `src/OpenClaw.FileWorker`: Sandboxed out-of-process file management worker.
9. `src/OpenClaw.Cli`: Command-line interface for gateway probing and WebSocket testing.
10. `src/OpenClaw.WinNode.Cli`: Headless Windows node companion CLI runner.
11. `src/OpenClaw.Tray.WinUI`: Primary WinUI 3 desktop tray application host.

### Test Projects (11)
1. `tests/OpenClaw.TestSupport`: Common mock filesystems, test loggers, assertion helpers.
2. `tests/OpenClaw.Shared.TestHost`: Out-of-process execution host for shared tests.
3. `tests/OpenClaw.GatewayFixtureHost`: Test gateway fixture runner.
4. `tests/OpenClaw.Shared.Tests`: Unit tests for cryptography, DPAPI, settings, and tokens.
5. `tests/OpenClaw.FileWorker.Tests`: Unit tests for sandboxed filesystem isolation.
6. `tests/OpenClaw.Connection.Tests`: Unit and integration tests for Gateway communication and lifecycle.
7. `tests/OpenClaw.WinNode.Cli.Tests`: Unit tests for winnode CLI argument parsing and dispatch.
8. `tests/OpenClaw.Tray.Tests`: Logic tests for tray viewmodels, routing, and lifecycle.
9. `tests/OpenClawTray.FunctionalUI.Tests`: Reactive state pipeline tests.
10. `tests/OpenClaw.Tray.IntegrationTests`: Subsystem integration tests.
11. `tests/OpenClaw.Tray.UITests`: WinUI 3 UI interaction tests.

---

## 3. Core Upstream Gateway Inventory

- **Entry Point**: `openclaw-main/openclaw.mjs` -> `src/entry.ts` -> `src/gateway/server.ts`
- **Build Distribution**: `openclaw-main/dist/` (compiled via `pnpm build`, 105 plugins packaged)
- **Local SQLite Store**: `~/.openclaw/agents/dev/agent/openclaw-agent.sqlite` (WAL mode enabled)
- **Supervision Mode**: `SparkManagedGatewayRuntime` manages child process via Win32 Job Object with `KILL_ON_JOB_CLOSE`.
- **Health Verification**: Polling loop against HTTP `http://127.0.0.1:18789/health`.

---

## 4. Initial Audit Findings & Baseline Observations

1. **Smart App Control (SAC) Environment Constraint**:
   - The host system runs Windows 11 with Smart App Control enforced (`VerifiedAndReputablePolicyState: 1`).
   - Self-signed binaries fail to load with `0x800711C7` unless trusted by Microsoft Root or possessing positive cloud reputation.
   - Clean, untampered framework-dependent binaries built from official .NET SDK execute without code integrity violations.
2. **Device Pairing Requirement**:
   - Upstream OpenClaw Gateway requires operator clients to undergo an explicit device pairing approval flow on initial connection.
   - When approved via `openclaw devices approve <requestId>`, the client is issued an Ed25519 device token stored in `%APPDATA%\OpenClawTray\device-key-ed25519.json`, allowing subsequent seamless reconnects.
3. **Agent Runtime Real Upstream Connection**:
   - Dispatched chat turns activate the genuine OpenClaw embedded agent runner, creating a SQLite WAL session record `agent:dev:main` and routing through upstream model provider integrations.
   - Without an active model provider API key, upstream requests cleanly produce `401 Unauthorized` errors from real provider endpoints rather than mock responses.

---

## 5. Verification Plan by Phase

- **Phase 1**: Clean Build Verification across all configurations.
- **Phase 2**: Gateway Process Lifecycle: Startup, Graceful Shutdown, Restart, Crash Recovery, Port Conflicts.
- **Phase 3**: Gateway Protocol: Handshake, Challenge-Response, Event Subscriptions, Reconnection Backoff.
- **Phase 4**: Agent Runtime: Real Model Provider, Tool Registration, Context Hydration, Multi-Turn Workflow.
- **Phase 5**: Chat UI: WinUI 3 Timeline, Markdown rendering, message streaming, cancellation.
- **Phase 6**: Tool Execution & MCP: In-process and MCP server tool dispatch with security approvals.
- **Phase 7**: Windows Node Capabilities: Screen, Camera, Canvas, Audio STT/TTS, Browser proxy.
- **Phase 8**: Persistence & State: SQLite sessions, DPAPI credentials, settings JSON schema migrations.
- **Phase 9**: Security Review: Localhost binding, token entropy, ACL checks, path traversal mitigation.
- **Phase 10**: Performance & Stability: Idle resource usage, memory footprint, socket leak checks.
- **Phase 11**: Installer Verification: Unpackaged and Inno Setup layout verification.
- **Phase 12**: Visual & UX Polish: DPI awareness, tray icon states, error diagnostics presentation.
