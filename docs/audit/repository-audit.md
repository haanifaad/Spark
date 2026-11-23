# Repository Audit - Project Spark

## 1. Overview & Context

Project Spark is a Windows-native transformation of the OpenClaw Windows companion ("Windows Hub") by deeply integrating the official OpenClaw Core Gateway runtime into a unified, self-contained desktop experience.

This audit assesses:
- **Repository A (Windows Hub)**: `C:\Users\PC\Documents\Spark\openclaw-windows-node-main`
- **Repository B (OpenClaw Core)**: `C:\Users\PC\Documents\Spark\openclaw-main`
- Host environment, toolchains, build systems, capabilities, and dependencies.

---

## 2. Repository A: Windows Hub (`openclaw-windows-node-main`)

### 2.1 Project Catalog & Targets

The solution is defined in `openclaw-windows-node.slnx` using .NET 10.0 and Windows App SDK 2.6.5:

| Project | Target Framework | Output Type | Primary Role |
|---|---|---|---|
| **`OpenClaw.Tray.WinUI`** | `net10.0-windows10.0.22621.0` | WinExe | Main desktop application. System tray flyout, WinUI 3 Workspace window (native Reactor chat, sessions, dashboards, automations, settings), local MCP HTTP server (`http://127.0.0.1:18793`), notification routing. |
| **`OpenClaw.Shared`** | `net10.0` | Class Library | Gateway clients (`OpenClawGatewayClient` for operator role, `WindowsNodeClient` for node role), `DeviceIdentity` (Ed25519 signing via BouncyCastle), 10+ node capabilities, local MCP bridge, MXC sandbox runner. |
| **`OpenClaw.Connection`** | `net10.0` | Class Library | Connection orchestrator (`GatewayConnectionManager`), `GatewayRegistry` (`gateways.json`), `CredentialResolver`, `DeviceIdentityStore`, `ConnectionStateMachine`, SSH tunneling. |
| **`OpenClaw.Chat`** | `net10.0` | Class Library | Chat data model (`ChatModels.cs`) and pure functional state reducer (`ChatTimelineReducer.cs`). |
| **`OpenClaw.SetupEngine`** | `net10.0` | Class Library | Automated installation engine: installs Ubuntu WSL distro (`OpenClawGateway`), sets up Node.js, systemd, generates tokens, pairs node. |
| **`OpenClaw.SetupEngine.UI`** | `net10.0-windows10.0.22621.0` | Class Library | WinUI 3 setup wizard UI pages and dialogs. |
| **`OpenClaw.WinNode.Cli`** | `net10.0` | Exe (`winnode.exe`) | Local command-line tool allowing automation/scripts to discover and invoke node capabilities via the tray app's MCP server. |
| **`OpenClaw.Cli`** | `net10.0` | Exe | Gateway WebSocket verification and diagnostic CLI. |
| **`OpenClawTray.FunctionalUI`** | `net10.0-windows10.0.22621.0` | Class Library | React-like declarative UI components for WinUI 3. |
| **`OpenClaw.FileWorker`** | `net10.0` | Exe | Isolated child process for high-integrity file transfer operations. |
| **`OpenClaw.GatewayCleanupHost`** | `net10.0` | Exe | Helper utility for pruning stale gateway records. |

### 2.2 Existing Gateway-Related Capabilities

- **Operator Client**: Implemented in `src/OpenClaw.Shared/OpenClawGatewayClient.cs` and `OpenClawGatewayClient.Protocol.cs`. Communicates over WebSocket with JSON-RPC-like frames (`type: "req"`, `type: "res"`, `type: "event"`). Supports streaming chat, session switching, aborting runs, event subscriptions, token metrics.
- **Node Client**: Implemented in `src/OpenClaw.Shared/WindowsNodeClient.cs`. Registers Windows node capabilities with the Gateway (`canvas`, `camera`, `screen`, `system`, `device`, `location`, `stt`, `tts`, `browser`, `file`). Handles `node.invoke.request` commands from the Gateway.
- **Cryptographic Device Identity**: `src/OpenClaw.Shared/DeviceIdentity.cs`. Generates Ed25519 keypairs, computes device ID as SHA-256 of public key, signs connect handshakes with V2 (`v2|...`) and V3 formats.
- **Current Gateway Hosting**: Relies on either an external remote gateway URL (`ws://...`) or an Ubuntu WSL-hosted gateway managed via WSL commands (`wsl.exe -d OpenClawGateway ...`).

### 2.3 Configuration & Persistence Paths

| Asset | Canonical Path | Description |
|---|---|---|
| Application Settings | `%APPDATA%\OpenClawTray\settings.json` | General app preferences, toggles, sandbox scope, speech models |
| Gateway Registry | `%APPDATA%\OpenClawTray\gateways.json` | Known gateways catalog, active selection, tokens |
| Device Identity Keys | `%APPDATA%\OpenClawTray\gateways\<id>\device-key-ed25519.json` | Ed25519 private/public keys and device ID |
| Exec Approvals | `%APPDATA%\OpenClawTray\exec-approvals.json` | Hash-based command approval catalog |
| Application Logs | `%LOCALAPPDATA%\OpenClawTray\openclaw-tray.log` | Rolling application logs |

---

## 3. Repository B: OpenClaw Core (`openclaw-main`)

### 3.1 Architecture & Gateway Runtime

- **Version**: `2026.11.3` (Monorepo pnpm workspace, 214 package projects).
- **Primary Entry Points**:
  - `openclaw.mjs` (CLI bootstrap launcher)
  - `dist/entry.js` (Compiled entry point, source `src/entry.ts`)
  - `src/cli/gateway-cli/run.ts` (Gateway CLI command handler)
  - `src/gateway/server.ts` & `src/gateway/server-start.ts` (Core Gateway server startup)
- **Networking Stack**:
  - HTTP Server: Built-in Node.js `node:http` / `node:https`.
  - WebSocket Server: `ws` package (`WebSocketServer` with `noServer: true`), attached to `httpServer.on("upgrade")`. Supports per-message deflate compression.
  - Default Listening Port: `18789` (configurable via `--port` or config).
- **Database Subsystem**:
  - Embedded SQLite using Node.js 24/26 standard library `node:sqlite` (`DatabaseSync` in WAL mode).
  - State database: `~/.openclaw/openclaw.db` (migrations up to schema v39).
  - Agent state: `~/.openclaw/agents/<agentId>/agent.db` (migrations v41).
  - Sessions: `~/.openclaw/sessions.json` and JSONL transcript files.
- **Agent Runtime & Orchestration**:
  - `src/agents/embedded-agent-runner/run-orchestrator.ts`: Central execution engine.
  - Multi-provider support: Claude, OpenAI, Gemini, Bedrock, Groq, Ollama, DeepSeek, Mistral, and custom OpenAI-compatible endpoints.
  - Tool execution engine: Dispatches tools locally, to MCP servers, or remotely to connected nodes via `node.invoke.request`.
- **Node-Host Protocol**:
  - Gateway WebSocket endpoint handles node connections via `src/gateway/server/ws-connection/connect-node-session.ts`.
  - Authenticates node via Ed25519 signed handshakes and node pairing store (`infra/device-pairing-node.ts`).
  - Remote tool execution routed to the Windows Node via `src/gateway/server-methods/nodes.invoke.ts`.

---

## 4. Host Environment & Toolchains Audit

| Tool | Required Version | Host Version / Status | Notes |
|---|---|---|---|
| **Operating System** | Windows 10 20H2+ / Windows 11 | Windows 11 AMD64 | Fully supported |
| **Node.js** | `>=24.16.0 <25 \|\| >=26.1.0` | `v26.13.0` | Satisfies requirement, includes WAL-safe `node:sqlite` |
| **npm** | `>=10.x` | `11.20.0` | Operational |
| **pnpm** | `>=9.x` | `12.18.0` | Installed and operational; `openclaw-main` pnpm install succeeded |
| **Python** | `>=3.10` | `3.12.10` | Operational |
| **WebView2** | Evergreen | `157.0.4341.46` | Installed; ready for WinUI chat and Canvas |
| **.NET SDK** | `10.0.400+` | `10.0.404` | Verified operational; builds solution |
| **Git** | `>=2.40` | `2.56.1.windows.1` | Verified operational; resolves repository metadata |
| **Windows SDK** | `10.0.26100.0+` | `10.0.28000.2957` (BuildTools) | Verified operational; compiles WinUI XAML assets |

---

## 5. Potential Conflicts & Integration Assessment

1. **Gateway Hosting Evolution**:
   - *Current Windows Hub*: Setup wizard offers "Install WSL gateway" or connects to external URL.
   - *Spark Integration*: In addition to WSL and external gateways, Spark introduces a **Managed Local Windows Native Gateway** directly launched, supervised, and monitored as a native background Node.js process by the Windows Hub without requiring WSL.
2. **WebSocket & Protocol Compatibility**:
   - Both repositories implement matching protocol semantics:
     - Operator role: `v2` / `v3` Ed25519 handshake, JSON-RPC streaming, session hub, cancel, history.
     - Node role: `node.invoke.request` with streaming chunks and result frames.
   - No protocol breaking changes are needed; client and server use the same wire protocol.
3. **Process Supervision & Lifecycle**:
   - The Gateway lifecycle manager in Windows Hub must use Windows Job Objects or Win32 process handles to ensure that when Spark exits or crashes, the Gateway process is cleanly terminated and never left orphaned.
4. **Configuration Convergence**:
   - The Windows Hub's settings file (`settings.json` / `gateways.json`) will register the managed local Windows Gateway endpoint (`ws://127.0.0.1:18789`), automatically provisioning tokens, certificates, and pairing on first run.
