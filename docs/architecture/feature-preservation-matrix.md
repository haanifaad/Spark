# Feature Preservation Matrix - Spark

This matrix tracks every Windows Hub and OpenClaw capability to guarantee no regressions occur during integration.

| Subsystem | Feature | Original Implementation | Spark Implementation | Status |
|---|---|---|---|---|
| **Desktop UI** | WinUI 3 Tray Icon | `OpenClaw.Tray.WinUI/TrayFlyoutWindow.xaml` | Preserved as Spark Tray Icon | **IMPLEMENTED** |
| **Desktop UI** | Workspace Chat Window | `OpenClaw.Tray.WinUI/Pages/ChatPage.xaml` | Preserved with direct local Gateway connection | **IMPLEMENTED** |
| **Desktop UI** | Settings Pages | `OpenClaw.Tray.WinUI/Pages/SettingsPage.xaml` | Preserved; adds Managed Gateway controls | **IMPLEMENTED** |
| **Desktop UI** | Setup Wizard | `OpenClaw.SetupEngine.UI/SetupWindow.xaml` | Adds "Start Local Managed Gateway" option | **IMPLEMENTED** |
| **Desktop UI** | Command Center | `OpenClaw.Tray.WinUI/Pages/CommandCenterPage.xaml` | Preserved | **IMPLEMENTED** |
| **Desktop UI** | Toast Notifications | `OpenClaw.Tray.WinUI/NotificationManager.cs` | Preserved | **IMPLEMENTED** |
| **Gateway Hosting**| WSL Gateway Setup | `OpenClaw.SetupEngine/SetupPipeline.cs` | Preserved as alternative setup path | **IMPLEMENTED** |
| **Gateway Hosting**| External Gateway Connect | `OpenClaw.Connection/GatewayConnectionManager.cs` | Preserved | **IMPLEMENTED** |
| **Gateway Hosting**| Managed Local Gateway | *New in Spark* | `SparkGatewayLifecycleManager` (Node.js child process) | **IMPLEMENTED** |
| **Protocol** | Operator WebSocket Client | `OpenClaw.Shared/OpenClawGatewayClient.cs` | Connects to local or remote Gateway | **IMPLEMENTED** |
| **Protocol** | Node WebSocket Client | `OpenClaw.Shared/WindowsNodeClient.cs` | Registers capabilities with Gateway | **IMPLEMENTED** |
| **Protocol** | Ed25519 Device Cryptography | `OpenClaw.Shared/DeviceIdentity.cs` | Preserved; V2/V3 signatures | **IMPLEMENTED** |
| **Capabilities**| System Shell Execution (`system.run`)| `OpenClaw.Shared/Mxc/NodeSandboxExecutor.cs` | Preserved with MXC container sandbox | **IMPLEMENTED** |
| **Capabilities**| Browser Control (`browser.*`) | `OpenClaw.Shared/Capabilities/BrowserProxyCapability.cs` | Preserved | **IMPLEMENTED** |
| **Capabilities**| Screen Capture (`screen.*`) | `OpenClaw.Shared/Capabilities/ScreenCapability.cs` | Preserved | **IMPLEMENTED** |
| **Capabilities**| Camera Capture (`camera.*`) | `OpenClaw.Shared/Capabilities/CameraCapability.cs` | Preserved | **IMPLEMENTED** |
| **Capabilities**| Location (`location.*`) | `OpenClaw.Shared/Capabilities/LocationCapability.cs` | Preserved | **IMPLEMENTED** |
| **Capabilities**| Speech-to-Text (`stt.*`) | `OpenClaw.Shared/Capabilities/SttCapability.cs` (Whisper.net) | Preserved | **IMPLEMENTED** |
| **Capabilities**| Text-to-Speech (`tts.*`) | `OpenClaw.Shared/Capabilities/TtsCapability.cs` (Sherpa/Kokoro) | Preserved | **IMPLEMENTED** |
| **Capabilities**| Canvas Presentation (`canvas.*`) | `OpenClaw.Shared/Capabilities/CanvasCapability.cs` (A2UI) | Preserved | **IMPLEMENTED** |
| **Integrations** | Local MCP HTTP Server | `OpenClaw.Shared/Mcp/McpHttpServer.cs` | Preserved on `http://127.0.0.1:18793` | **IMPLEMENTED** |
| **Integrations** | `winnode` CLI Tool | `OpenClaw.WinNode.Cli` | Preserved | **IMPLEMENTED** |
| **Agent Runtime**| Multi-Provider LLMs | Upstream `src/agents/` | Driven by managed Gateway | **IMPLEMENTED** |
| **Agent Runtime**| Streaming Chat & Reasonings | Upstream `src/agents/` -> Gateway WS -> `OpenClaw.Chat` | Preserved | **IMPLEMENTED** |
| **Agent Runtime**| SQLite Persistence | Upstream `node:sqlite` WAL | Preserved in `openclaw.db` | **IMPLEMENTED** |
