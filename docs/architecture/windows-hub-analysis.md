# Windows Hub Architecture Analysis - Project Spark

## 1. System Structure

The OpenClaw Windows Hub repository (`openclaw-windows-node-main`) provides the native Windows desktop shell, device capability integration, and Gateway client communication layer.

It is structured into 11 .NET 10.0 projects:

```
src/
├── OpenClaw.Tray.WinUI/            [WinExe, net10.0-windows10.0.22621.0]
│   Desktop entry point, WinUI 3 UI, Reactor Chat, Dashboards, Tray flyout
├── OpenClaw.Shared/                [ClassLib, net10.0]
│   Gateway clients, DeviceIdentity (Ed25519), Node capabilities, MCP bridge
├── OpenClaw.Connection/            [ClassLib, net10.0]
│   GatewayConnectionManager, gateways.json, CredentialResolver, Lifecycle
├── OpenClaw.Chat/                  [ClassLib, net10.0]
│   Chat data model and pure functional state reducer (ChatTimelineReducer)
├── OpenClawTray.FunctionalUI/      [ClassLib, net10.0-windows10.0.19041.0]
│   Declarative UI components for WinUI 3
├── OpenClaw.SetupEngine/           [ClassLib, net10.0]
│   Automated setup orchestration (WSL, prerequisites, token generation)
├── OpenClaw.SetupEngine.UI/        [ClassLib, net10.0-windows10.0.22621.0]
│   WinUI 3 setup wizard UI pages and controls
├── OpenClaw.FileWorker/            [Exe, net10.0]
│   Isolated high-integrity file transfer worker process
├── OpenClaw.Cli/                   [Exe, net10.0]
│   Operator WebSocket diagnostic CLI
├── OpenClaw.WinNode.Cli/           [Exe, net10.0]
│   Command-line node capability invoker via local MCP
└── OpenClaw.GatewayCleanupHost/    [Exe, net48]
    Process cleanup utility
```

---

## 2. Key Component Deep Dive

### 2.1 Application Entry Point (`OpenClaw.Tray.WinUI`)
- **`App.xaml.cs` / `Program.cs`**:
  - Initializes single-instance mutex.
  - Registers dependency injection services (`ServiceCollection`).
  - Boots tray icon flyout (`TrayFlyoutWindow.xaml`).
  - Manages Workspace window lifecycle (shows on tray click or hotkey).
  - Starts background services: `GatewayConnectionManager`, `McpHttpServer`, `NotificationManager`.

### 2.2 Connection Manager (`OpenClaw.Connection`)
- **`GatewayConnectionManager.cs`**:
  - Central orchestrator of Gateway connectivity.
  - Monitors connection state via `ConnectionStateMachine.cs`.
  - Routes through `NativeGatewayRuntimeRouter` to supervise managed local Gateway instances.
  - Instantiates and connects both `OpenClawGatewayClient` (Operator role) and `WindowsNodeClient` (Node role).
  - Manages automatic reconnection with exponential backoff and jitter.

### 2.3 Gateway Client & Protocols (`OpenClaw.Shared`)
- **`OpenClawGatewayClient.cs`**:
  - WebSocket client implementing the OpenClaw Operator protocol.
  - Subscribes to chat streams, session runs, agent events, and approvals.
  - Correlates RPC request IDs to responses (`req`/`res`).
- **`WindowsNodeClient.cs`**:
  - WebSocket client implementing the OpenClaw Node role.
  - Registers hardware and OS capabilities: `canvas`, `camera`, `screen`, `system`, `device`, `location`, `stt`, `tts`, `browser`, `file`.
  - Listens for `node.invoke.request` commands from the Gateway and streams back results.

### 2.4 Device Identity & Cryptography
- **`DeviceIdentity.cs`**:
  - Generates Ed25519 keypairs using BouncyCastle.
  - SHA-256 digests of public keys identify the Windows node.
  - Signs handshake envelopes using V2 and V3 protocol specifications.

### 2.5 Native Chat Surface (`OpenClaw.Chat` & `OpenClaw.Tray.WinUI/Chat`)
- Built using Microsoft UI Reactor (declarative C# UI for WinUI 3).
- Supports streaming markdown, thinking blocks, tool calls, user questions, and slash commands.
- High-performance virtualized message timeline (`ReactorChatTimeline.cs`).

### 2.6 Local MCP Server
- **`McpHttpServer.cs`**:
  - Embedded HTTP server listening on `http://127.0.0.1:18793`.
  - Exposes node capabilities to external tools and CLI automation (`winnode.exe`).

---

## 3. Existing Gateway Management vs. Spark Enhancements

| Capability | Upstream Windows Hub | Spark Integrated Solution |
|---|---|---|
| Remote Gateway | Supported via WebSocket URL | Preserved |
| WSL Gateway | Supported via WSL bash commands | Preserved as optional path |
| Local Native Gateway | Stubbed package router | **Fully integrated Node.js child process (`SparkManagedGatewayRuntime`) with Windows Job Object supervision** |
| Process Containment | Basic job assignment | Win32 Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` ensures zero orphans |
| Startup Probing | Basic TCP port check | HTTP `/health` JSON probe with timeout and graceful restart |
