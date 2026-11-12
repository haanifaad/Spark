# Feature Compatibility Matrix - Project Spark

This document tracks compatibility between upstream OpenClaw features and the Spark Windows application.

| Feature Name | Upstream Source Location | Required Dependencies | Spark Implementation | Test Method | Test Result | Status | Known Limitations |
|---|---|---|---|---|---|---|---|
| **Gateway WebSocket Server** | `openclaw-main/src/gateway/server.ts` | `ws`, `node:http` | Node.js child process managed by `SparkManagedGatewayRuntime` | Automated HTTP/WS health check on `127.0.0.1:18789` | Passed | **VERIFIED** | Loopback binding only by default |
| **Operator Client Protocol** | `openclaw-main/src/gateway/server/ws-connection/` | `@openclaw/gateway-protocol` | `OpenClaw.Shared/OpenClawGatewayClient.cs` | End-to-end WebSocket connect, handshake, and session query | Passed | **VERIFIED** | Requires valid Ed25519 token/signature |
| **Node Hardware Capabilities** | `openclaw-main/src/node-host/` | Node-Host protocol | `OpenClaw.Shared/WindowsNodeClient.cs` | Capability registration and `node.invoke.request` dispatch | Passed | **VERIFIED** | User approval needed for sensitive commands |
| **Agent Runtime Engine** | `openclaw-main/src/agents/embedded-agent-runner/` | `@openclaw/agent-core`, LLM providers | Hosted inside managed OpenClaw Gateway | End-to-end agent turn with model invocation | Passed | **VERIFIED** | Valid model API key or local Ollama needed |
| **SQLite State Persistence** | `openclaw-main/src/state/openclaw-state-db.ts` | `node:sqlite` (Node 24/26) | `~/.openclaw/openclaw.db` in WAL mode | Process restart persistence validation | Passed | **VERIFIED** | Requires Node.js with `node:sqlite` support |
| **WinUI 3 Reactor Chat UI** | `openclaw-windows-node-main/src/OpenClaw.Tray.WinUI/Chat` | Microsoft.UI.Reactor | WinUI 3 desktop chat interface | Compilation and UI launch validation | Passed | **VERIFIED** | Requires Windows App SDK runtime |
| **Job Object Supervision** | N/A (Windows native) | Win32 API (`CreateJobObjectW`) | `OpenClaw.Connection/NativeGateway/WindowsNativeGatewayProcessHost.cs` | Crash and termination tests | Passed | **VERIFIED** | Windows-only API |
| **Local MCP HTTP Server** | `openclaw-windows-node-main/src/OpenClaw.Shared/Mcp/` | `HttpListener` | `OpenClaw.Shared/Mcp/McpHttpServer.cs` on port 18793 | MCP tool enumeration request | Passed | **VERIFIED** | Port 18793 must be free |
| **Browser Control** | `openclaw-windows-node-main/src/OpenClaw.Shared/Capabilities/BrowserProxyCapability.cs` | Edge/Chromium | CDP browser automation capability | Capability registration test | Passed | **VERIFIED** | Edge/Chrome installed |
| **Camera & Screen Capture** | `openclaw-windows-node-main/src/OpenClaw.Shared/Capabilities/` | WinRT MediaCapture, GraphicsCapture | Native Windows media APIs | Capability registration test | Passed | **VERIFIED** | Camera hardware required for camera |
| **Speech-to-Text (STT)** | `openclaw-windows-node-main/src/OpenClaw.Shared/Capabilities/SttCapability.cs` | Whisper.net | App-local Whisper model | Native engine initialization test | Passed | **VERIFIED** | Model weights downloaded on demand |
| **Text-to-Speech (TTS)** | `openclaw-windows-node-main/src/OpenClaw.Shared/Capabilities/TtsCapability.cs` | sherpa-onnx | App-local Piper voice models | Native engine initialization test | Passed | **VERIFIED** | VC++ 14.38+ runtime recommended |
