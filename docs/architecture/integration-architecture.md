# Spark Integration Architecture

## 1. System Vision

Spark transforms the OpenClaw Windows Hub from an external/WSL companion into an independently launchable, self-contained Windows-native OpenClaw application.

```
+-----------------------------------------------------------------------------------+
|                            SPARK WINDOWS APPLICATION                              |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  +-------------------------------------+  +------------------------------------+  |
|  |           NATIVE WINUI 3 UI         |  |        WINDOWS HUB SERVICES        |  |
|  |  • System Tray Flyout               |  |  • WindowsNodeClient (Node Role)   |  |
|  |  • Workspace Window (Chat, Sessions)|  |  • OpenClawGatewayClient (Op Role) |  |
|  |  • Settings & Config Editor         |  |  • 10+ Device Capabilities        |  |
|  |  • Local MCP HTTP Server            |  |  • MXC Container Sandbox Runner    |  |
|  |  • Native Notifications & Hotkeys   |  |  • Ed25519 Device Cryptography    |  |
|  +------------------▲------------------+  +------------------▲-----------------+  |
|                     │                                        │                    |
|                     │             WebSocket RPC              │                    |
|                     └──────────────────┬─────────────────────┘                    |
|                                        │ ws://127.0.0.1:18789                     |
|                                        ▼                                          |
|  +-----------------------------------------------------------------------------+  |
|  |                         SPARK GATEWAY LIFECYCLE MANAGER                     |  |
|  |  (C# In-Process Supervisor in OpenClaw.Connection / OpenClaw.Tray.WinUI)    |  |
|  |  • Runtime Discovery & Version Check (Bundled Node.js / System Node.js)     |  |
|  |  • Process Lifecycle (Start, Stop, Graceful Terminate, Restart)            |  |
|  |  • Windows Job Object (Auto-termination on Spark exit, No Orphan Processes)|  |
|  |  • Loopback Port Reservation & Collision Detection                          |  |
|  |  • Health Monitoring & Automatic Crash Recovery                            |  |
|  |  • Standard Output / Error Stream Redirection to Rolling Diagnostics Logs  |  |
|  +-------------------------------------┬---------------------------------------+  |
|                                        │ Spawns & Supervises                      |
|                                        ▼                                          |
|  +-----------------------------------------------------------------------------+  |
|  |                     MANAGED OPENCLAW GATEWAY RUNTIME                        |  |
|  |  (Bundled Node.js 26.x + OpenClaw Core Distribution)                        |  |
|  |  • HTTP / WebSocket Server (node:http, ws)                                  |  |
|  |  • Agent Runtime & Orchestrator (run-orchestrator.ts)                       |  |
|  |  • Model Providers (Claude, OpenAI, Gemini, Local Ollama)                   |  |
|  |  • Tool System & Local Skills Engine                                        |  |
|  |  • Session Controller & Transcripts Manager                                 |  |
|  |  • Embedded SQLite Engine (node:sqlite WAL mode)                            |  |
|  +-----------------------------------------------------------------------------+  |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 2. Key Architectural Decisions

### Decision 1: Gateway Runtime Model
- **Choice**: Managed in-process child process running Node.js.
- **Rationale**: Rewriting the entire OpenClaw monorepo in C# or Rust would discard active upstream development, plugin ecosystems, and security patches. Running Node.js as a supervised child process preserves 100% upstream fidelity while delivering a native single-window Windows application.

### Decision 2: Process Lifecycle & Orphan Prevention
- **Choice**: Windows Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.
- **Implementation**: In C# (`SparkGatewayProcessManager`), create a Win32 Job Object, assign the child Node.js gateway process to the job, and configure the OS to automatically terminate the entire process tree if Spark crashes or terminates.

### Decision 3: IPC & Networking
- **Choice**: Dedicated loopback WebSocket on `127.0.0.1:18789`.
- **Rationale**: Reuses the tested, high-performance `OpenClawGatewayClient` and `WindowsNodeClient` without changing wire protocol semantics.

### Decision 4: Safe Configuration Coexistence
- **Choice**: Store Gateway runtime state under `%APPDATA%\OpenClawTray\gateway-runtime` (or `~/.openclaw`), keeping credentials safe in Windows Credential Locker / encrypted storage. Automatically pair the native Windows Hub on first startup.

### Decision 5: Support External Gateways
- **Choice**: The existing ability to connect to remote or WSL gateways is preserved. Spark adds "Managed Local Gateway" as the default, first-class connection target.
