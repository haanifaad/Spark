# Project Spark - Compatibility & Protocol Report

## 1. Gateway Protocol Wire Compatibility

### 1.1. Protocol Version and Contract
- **Protocol Baseline**: OpenClaw Gateway Protocol 2026.11.3 (Upstream Git rev / npm package).
- **Transport**: JSON-RPC 2.0 / framed JSON over WebSockets (`ws://127.0.0.1:18789`).
- **Encoding**: UTF-8 encoded text frames.

### 1.2. Role Negotiation & Client Multiplexing
Spark connects two distinct client roles over loopback to the managed OpenClaw Gateway:

```text
┌─────────────────────────────────────────────────────────────┐
│                      Spark Desktop (WinUI 3)                │
│                                                             │
│   ┌──────────────────────────┐  ┌────────────────────────┐  │
│   │ OpenClawGatewayClient    │  │ WindowsNodeClient      │  │
│   │ (Role: "operator")       │  │ (Role: "node")         │  │
│   └────────────┬─────────────┘  └───────────┬────────────┘  │
└────────────────┼────────────────────────────┼───────────────┘
                 │ ws://127.0.0.1:18789       │ ws://127.0.0.1:18789
                 ▼                            ▼
┌─────────────────────────────────────────────────────────────┐
│                 Managed OpenClaw Gateway                    │
│                 (Node.js Kernel Engine)                     │
└─────────────────────────────────────────────────────────────┘
```

1. **Operator Role (`OpenClawGatewayClient`)**:
   - Manages user sessions, chat message dispatch (`chat.send`), streaming response ingestion (`chat.stream`), command catalog queries (`commands.list`), and system configuration.
   - Signs connect challenge with device key (`DeviceIdentityConnectEnvelopeSigner`).
2. **Node Role (`WindowsNodeClient`)**:
   - Registers Windows-specific capabilities (`canvas.*`, `camera.*`, `screen.capture`, `system.run`).
   - Handles `node.invoke.request` and sends streaming progress/results (`node.invoke.progress`, `node.invoke.result`).

---

## 2. Windows 11 Smart App Control (SAC) Compatibility

### 2.1. Host Assessment
- **Operating System**: Windows 11 Enterprise (Build 26100.x / 24H2).
- **SAC Enforcement**: Active (`SmartAppControlState: On`). Under SAC, unsigned dynamic libraries or binaries from untrusted sources are blocked from execution (Win32 error `0x800711C7`).

### 2.2. Architectural Compatibility Measures
1. **Managed Interpreted Gateway Execution**:
   - The Gateway executes as an interpreted Node.js script tree using the verified platform Node.js binary (`node.exe`). This avoids generating unsigned native binary PE headers that would trigger SAC policy blocks.
2. **Build Toolchain Roslyn Policy**:
   - The `Reactor.SourceMap.Generator.dll` analyzer package triggers SAC CS8034 during debug builds when unsigned.
   - The Spark build configuration incorporates `-p:ReactorSourceMap=false -p:ReactorDiagnostics=false`, preventing unauthorized analyzer execution while keeping all functional runtime code intact.
3. **AppHost and Runtime Discovery**:
   - Windows desktop AppHosts locate .NET 10 via user-level environment variables (`DOTNET_ROOT` / `DOTNET_ROOT_X64`) without requiring machine-wide administrator registry modifications.

---

## 3. Native Windows Capability Matrix

| Capability Category | Protocol Command | Windows Native Provider | Status |
| :--- | :--- | :--- | :--- |
| **System Diagnostics** | `system.which`, `system.info` | Win32 Process / System API | **Preserved (Full Parity)** |
| **Command Execution** | `system.run` | `NodeSandboxExecutor` (MXC Sandbox) | **Preserved with Policy Approval** |
| **Screen Capture** | `screen.capture` | Windows Graphics Capture / GDI+ | **Preserved** |
| **Camera Access** | `camera.capture` | Windows MediaCapture / DirectShow | **Preserved** |
| **Canvas UI** | `canvas.navigate`, `canvas.show` | Microsoft Edge WebView2 Control | **Preserved** |
| **Speech STT** | `audio.transcribe` | Silero VAD + Whisper / Windows Speech | **Preserved (App-local runtime)** |
| **Speech TTS** | `audio.speak` | Piper / Kokoro / ElevenLabs / MiniMax | **Preserved** |
| **MCP Bridge** | `mcp.proxy` | Local MCP HTTP / SSE Server | **Preserved** |

---

## 4. Lifecycle & Process Supervision Matrix

| Scenario | Expected Behavior | Verification Mechanism | Observed Outcome |
| :--- | :--- | :--- | :--- |
| **App Startup** | Automatically launch managed Gateway on port 18789 | `SparkManagedGatewayRuntime.EnsureRunningAsync` | **PASS**: Gateway up in < 2.5s |
| **Health Probe** | Return HTTP 200 `{"ok":true,"status":"live"}` | Polling `http://127.0.0.1:18789/health` | **PASS**: Verified live |
| **App Crash / Kill** | Automatic child process cleanup (zero orphan processes) | Win32 Job Object `KILL_ON_JOB_CLOSE` | **PASS**: Verified by PID teardown |
| **App Normal Exit** | Graceful shutdown via SIGTERM/Job termination | `SparkManagedGatewayRuntime.StopAsync` | **PASS**: Port 18789 cleanly released |
| **Port Conflict** | Refuse to send credentials to unverified listener | `NativeGatewayEndpointSecurity.AuthorizeAsync` | **PASS**: Alert raised, credentials safe |
