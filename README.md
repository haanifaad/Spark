# SPARK  -  Native Windows AI Assistant & Agent Hub

<p align="center">
  <strong>An intelligent, native Windows desktop application and autonomous agent hub.</strong><br>
  <em>Created by <strong>HAANI</strong></em>
</p>

<p align="center">
  <a href="https://github.com/haanifaad/Spark"><img src="https://img.shields.io/badge/Release-v1.0.0-blue?style=flat-square" alt="Release"></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square" alt=".NET 10"></a>
  <a href="https://learn.microsoft.com/windows/apps/windows-app-sdk/"><img src="https://img.shields.io/badge/Windows%20App%20SDK-2.6.5-0078d4?style=flat-square" alt="Windows App SDK"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green?style=flat-square" alt="MIT License"></a>
</p>

---

## 1. Introduction

**SPARK** is a native Windows desktop application that pairs a WinUI 3 desktop client with a managed, supervised **OpenClaw Gateway** and **Agent Runtime**. Developed by **HAANI**, SPARK enables local AI chat, autonomous task execution, and system-level capability delegation with safety guardrails.

SPARK operates directly on Windows without requiring WSL (Windows Subsystem for Linux), running an embedded Node.js Gateway supervisor bound to Win32 Job Objects for zero-orphan lifecycle management.

---

## 2. Key Features

- **Native Windows Desktop Experience**: Built with .NET 10 and Windows App SDK / WinUI 3, featuring Fluent Design, Mica material, dark/light theme awareness, and system tray integration.
- **Managed Gateway Lifecycle Supervisor**: Automatic discovery, launch, health-probing, and termination of the local Node.js OpenClaw Gateway.
- **Process Isolation & Safety**: Uses Windows Win32 Job Objects with `KILL_ON_JOB_CLOSE` (`0x2000`) to guarantee that child Gateway and agent worker processes never orphan on exit.
- **Multi-Model Provider Integration**: Native support for local Ollama models (`qwen2.5-coder`, `llama3`), Anthropic Claude, OpenAI, Google Gemini, Groq, Mistral, and custom OpenAI-compatible endpoints.
- **Rich Chat Interface**: Reactive Markdown chat rendering with real-time streaming, syntax-highlighted code blocks, tool activity feeds, and session history management.
- **Windows Node Capabilities**:
  - **Canvas**: Interactive visual rendering using Microsoft Edge WebView2.
  - **Camera**: Photo snapshots and clips via Windows `MediaCapture`.
  - **Screen Capture**: Multi-monitor desktop screenshots and window capture via GDI+ and Windows Graphics Capture.
  - **Sandbox Execution**: MXC isolated process sandboxing with configurable strict, workspace, or open isolation levels.
  - **Audio Speech**: Local Text-to-Speech (Piper / Kokoro) and Speech-to-Text (Whisper / Sherpa-ONNX).
  - **Model Context Protocol (MCP)**: Local MCP HTTP server bridge (`http://127.0.0.1:18793`) for seamless tool discovery and invocation.
- **Cryptographic Security**: Ed25519 device authentication handshakes, local DPAPI-encrypted credential storage, and hash-verified command execution approvals.

---

## 3. Architecture Overview

SPARK combines a high-performance native Windows client with an asynchronous Node.js Agent Runtime:

```text
+-------------------------------------------------------------------------+
|                              SPARK (WinUI 3)                            |
|                                                                         |
|  +--------------------+  +----------------------+  +-----------------+  |
|  |   System Tray      |  |   Workspace Window   |  | Command Center  |  |
|  |   Flyout & Menu    |  |   (Reactor Chat UI)  |  | & Permissions   |  |
|  +---------+----------+  +----------+-----------+  +--------+--------+  |
|            |                        |                       |           |
|            +-------------------+----+-----------------------+           |
|                                |                                        |
|              +-----------------v-----------------+                      |
|              |     SparkManagedGatewayRuntime    |                      |
|              |   (Process & Job Object Supervisor)|                     |
|              +-----------------+-----------------+                      |
+--------------------------------|----------------------------------------+
                                 |
                 HTTP /health    |  WebSocket ws://127.0.0.1:18789
                 Probe & Auth    |  (Ed25519 Handshake / JSON-RPC)
                                 v
+-------------------------------------------------------------------------+
|                  Managed OpenClaw Gateway & Agent Runtime                |
|                                                                         |
|  +--------------------+  +----------------------+  +-----------------+  |
|  |   Agent Runner     |  |   SQLite Database    |  | Tool Dispatcher |  |
|  | (Ollama / Claude)  |  | (node:sqlite WAL DB) |  | & MCP Bridge    |  |
|  +--------------------+  +----------------------+  +-----------------+  |
+-------------------------------------------------------------------------+
```

---

## 4. System Prerequisites

### For End-Users
- **Operating System**: Windows 11 (64-bit x64) or Windows 10 Version 2004+ (Build 19041+).
- **Node.js**: Version 22.x LTS or Version 26.x (recommended) with native `node:sqlite`.
- **Microsoft Edge WebView2**: Preinstalled on Windows 11; installed automatically via Evergreen installer if missing.
- **Visual C++ Redistributable**: 2015-2022 x64 CRT runtime (v14.38+).

### For Developers
- **.NET SDK**: .NET 10.0 SDK (`10.0.400` or higher).
- **Windows SDK**: Windows 10 SDK `10.0.26100.0` or higher.
- **Node.js & Package Manager**: Node.js v26.13.0 and pnpm 12.x.
- **Inno Setup**: Inno Setup 6 (`ISCC.exe`) for compiling the distribution installer.

---

## 5. Building from Source

### Clone the Repository
```powershell
git clone https://github.com/haanifaad/Spark.git
cd Spark
```

### Build the Desktop Application
```powershell
# Restore dependencies and build in Release mode
.\build.ps1 -Configuration Release
```

### Run Tests
```powershell
# Execute unit and lifecycle integration tests
dotnet test tests\OpenClaw.Connection.Tests\OpenClaw.Connection.Tests.csproj `
  --filter "FullyQualifiedName~SparkManagedGateway"
```

---

## 6. Building the Windows Installer

SPARK uses Inno Setup 6 to compile a standalone, single-file Windows installer:

```powershell
# Compile the production installer for x64
.\scripts\build-inno-local.ps1 -Arch x64 -Fast
```

The compiled installer will be located at:
```text
Output\SPARK-Setup-x64.exe
```

### Installer Capabilities
- Installs to `%LOCALAPPDATA%\OpenClawTray` without requiring administrator privileges.
- Registers Start Menu shortcuts (`SPARK`, `SPARK Chat`, `SPARK Settings`).
- Offers an optional Desktop shortcut.
- Registers the `openclaw://` custom protocol handler for system activation.
- Preserves existing user configurations (`settings.json`, device keys, sessions) across upgrades.
- Provides a clean, registered Windows uninstaller in **Installed apps**.

---

## 7. Known Limitations

- **Platform Target**: Designed and tested specifically for Windows 64-bit (`win-x64`). ARM64 compilation is supported via `-Arch arm64`.
- **Local Loopback Isolation**: The managed Gateway binds strictly to loopback (`127.0.0.1:18789`) by default for host security.
- **Model API Keys**: Cloud model providers require valid API keys entered via the Settings page or configured in the OpenClaw environment.

---

## 8. Attribution & Open-Source Licensing

**SPARK** is created and maintained by **HAANI**.

SPARK incorporates and adapts open-source components from the **OpenClaw** project and **OpenClaw Windows Node** (`openclaw-windows-node`), published under the MIT License by the OpenClaw Foundation and its contributors.

This project is an independent distribution and is **not an official product of the OpenClaw Foundation**. All upstream copyrights, trademarks, and license notices have been preserved in full accordance with the MIT License.

See [LICENSE](LICENSE) and [docs/release/dependency-audit.md](docs/release/dependency-audit.md) for complete license text and upstream attribution.
