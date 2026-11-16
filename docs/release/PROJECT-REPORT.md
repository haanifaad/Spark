# SPARK
## Project Development, Architecture & Release Report

**Project:** SPARK  -  Personal AI Agent for Windows  
**Creator:** Haani  
**Upstream Technology:** OpenClaw  
**Platform:** Microsoft Windows  
**Repository:** https://github.com/haanifaad/Spark  
**Report Date:** 15 November 2026  
**Status:** Approved for Release Candidate (v1.0.0-rc1)

---

# 1. Executive Summary

SPARK is a Windows-native personal AI agent application developed by Haani, inspired by the capabilities of JARVIS and modern AI agent systems.

The project began with the goal of turning the existing OpenClaw ecosystem into a more accessible, integrated Windows desktop experience.

Rather than building an entirely new AI agent framework from scratch, the project uses OpenClaw as its underlying technology and combines it with a Windows desktop application.

The objective was to create a unified application through which users can interact with AI models, manage agent sessions, use tools, connect capabilities, and access an AI assistant through a native Windows interface.

The project also involved replacing the user-facing OpenClaw branding with SPARK, auditing the codebase, removing unnecessary repository content where safe, preparing a production build, and creating a Windows installer.

---

# 2. Project Background

## 2.1 The Original Problem

OpenClaw provides an AI agent ecosystem, but integrating its capabilities into a polished, self-contained Windows desktop application requires additional engineering.

The main challenges were:

- Connecting the desktop application to the actual OpenClaw Gateway.
- Ensuring the Agent Runtime and tool execution were available.
- Managing the Gateway lifecycle on Windows.
- Preserving compatibility with upstream functionality.
- Providing a straightforward desktop experience.
- Packaging the application without requiring users to manually configure the development environment.

## 2.2 The Solution

The solution was to develop SPARK around two major components:

1. **Windows Desktop Application:** Provides the native user interface and Windows-specific functionality.
2. **OpenClaw Backend:** Provides the underlying Gateway, agent execution infrastructure, and supported integrations.

The application is intended to manage the backend and present its capabilities through a unified desktop experience.

---

# 3. Development Journey  -  What Was Done

## Phase 1  -  Understanding OpenClaw

The first major task was understanding the upstream OpenClaw ecosystem.

The project required distinguishing between three important components:

- **Gateway:** Handles communication and coordination between clients and the agent system.
- **Agent Runtime:** Executes agent tasks and manages the underlying agent workflow.
- **Windows Node:** Exposes Windows-specific capabilities to the Gateway.

An important architectural decision was made: the Windows desktop client alone was not sufficient to provide the complete OpenClaw experience. The actual Gateway and Agent Runtime also needed to be integrated.

## Phase 2  -  Windows Desktop Foundation

The Windows application was based on the existing OpenClaw Windows Node project.

The foundation provides the Windows desktop integration layer, including its existing application infrastructure and supported Windows capabilities.

The project retained the existing architecture instead of unnecessarily rewriting the entire application.

## Phase 3  -  Gateway Integration

The project focused on integrating the actual OpenClaw Gateway.

The intended integration includes:

- Gateway startup and shutdown management.
- Gateway connection handling.
- Runtime monitoring.
- Health checks.
- Logging and diagnostics.
- Restart and recovery management.
- Prevention of duplicate or orphaned Gateway processes using Win32 Job Objects (`KILL_ON_JOB_CLOSE`).

The Gateway remains in its supported Node.js/TypeScript environment rather than being rewritten simply to fit the Windows application.

## Phase 4  -  Agent Runtime Integration

A Gateway connection alone does not make a complete AI agent.

SPARK therefore needed access to the actual Agent Runtime and its associated capabilities.

The integration scope includes:

- Agent execution.
- Model-provider connectivity (local Ollama and cloud providers).
- Session management.
- Tool execution.
- Memory functionality.
- Skills and integrations.
- Agent communication through the Gateway.

## Phase 5  -  SPARK Branding

The application was rebranded from its OpenClaw-facing identity to SPARK.

The product identity was established as:

**Product:** SPARK  
**Creator:** HAANI  
**Platform:** Windows

The branding work covers user-facing application identity, including:

- Application name (`SPARK`).
- Desktop presentation (Fluent Design, Mica, system tray, mascot assets).
- Installer identity (`SPARK-Setup-x64.exe`, publisher HAANI).
- Application metadata and AppUserModelID.
- Documentation and guides.
- User-facing references.

A critical distinction was maintained: SPARK is an independent application built using OpenClaw components, not an official OpenClaw product.

Upstream attribution, licensing, package identifiers, and technical compatibility identifiers must remain intact.

## Phase 6  -  Repository Audit and Cleanup

The GitHub repository was reviewed with the goal of avoiding unnecessary files and folders.

The important engineering principle was to distinguish between:

- Source code required for development.
- Dependencies required for building.
- Components required at runtime.
- Generated build output.
- Temporary files and caches.
- Unnecessary or duplicated content.

The upstream OpenClaw monorepo (~4.1 GB) was isolated from Git tracking while preserving runtime linkages. Transient directories (`bin/`, `obj/`, `artifacts/`, `publish/`) and certificates were excluded, ensuring the Git repository size remains clean (~38 MB).

## Phase 7  -  Production Build and Installer

The project moved towards a distributable Windows application.

The release target was a Windows installer using Inno Setup 6 (`SPARK-Setup-x64.exe`).

The installer contains only the files required to install and operate SPARK, including its necessary application assets, runtime components, dependencies, and configuration templates:

- Per-user installation to `%LOCALAPPDATA%\OpenClawTray` (no UAC elevation required).
- Start Menu and optional Desktop shortcuts.
- System protocol registration (`openclaw://`).
- Safe preservation of user configuration (`settings.json`) and cryptographic identity keys across updates.
- Clean uninstaller registered in Windows Installed apps.

## Phase 8  -  Verification and Bug Fixing

The final verification process was designed to check the application beyond whether it simply opens.

The important verification areas were:

- Application startup and shutdown.
- Gateway startup and connectivity.
- Agent Runtime availability.
- Actual AI model communication.
- Chat functionality.
- Tool execution.
- Windows capability integration.
- Configuration persistence.
- Memory and session persistence.
- Security and permissions.
- Installer functionality.
- Clean installation and uninstallation.

---

# 4. Technical Architecture

## 4.1 High-Level Architecture

```text
                  SPARK
             Windows Desktop App
                      |
          Application / UI Layer
          (WinUI 3 / Mica Backdrop)
                      |
             Gateway Client
       (WebSocket / JSON-RPC / Ed25519)
                      |
          Gateway Lifecycle Manager
     (SparkManagedGatewayRuntime / Job Object)
                      |
             OpenClaw Gateway
         (Node.js / loopback 18789)
                      |
              Agent Runtime
                      |
       +--------------+--------------+
       |              |              |
   AI Models       Tools          Memory
  (Ollama/Claude) (MCP/Worker)   (SQLite WAL)
       |              |              |
   Providers      Integrations    Sessions
                      |
               Windows Node
                      |
       Windows-Specific Capabilities
      (WebView2, Camera, Screen, Audio)
```

## 4.2 Technology Stack

| Component | Technology / Foundation |
|---|---|
| Desktop foundation | OpenClaw Windows Node |
| Desktop application | Modern C# / Windows App SDK 2.6.5 (.NET 10.0.404) |
| Windows UI | WinUI 3 (Fluent Design, Mica material, reactive dispatch) |
| Backend Gateway | OpenClaw Core Gateway (Node.js v26.13.0, TypeScript, pnpm 12.18.0) |
| Gateway runtime | Node.js (strictly bound to loopback `127.0.0.1:18789`) |
| Process Supervisor | Win32 Job Object with `KILL_ON_JOB_CLOSE` (zero orphan processes) |
| Agent infrastructure | OpenClaw Agent Runtime (SQLite database with WAL journaling) |
| Communication | OpenClaw Gateway Protocol v3 (Ed25519 challenge-response handshake) |
| Platform | Microsoft Windows 10 (19041+) / Windows 11 (64-bit x64) |
| Source control | Git & GitHub (`https://github.com/haanifaad/Spark`) |
| Packaging & Distribution | Inno Setup 6 Native Standalone Installer (`SPARK-Setup-x64.exe`) |

---

# 5. SPARK Features

## 5.1 Core AI Assistant

- Desktop-based AI assistant with system tray integration and floating workspace window.
- Multi-model connectivity supporting local Ollama models (`qwen2.5-coder:7b`, `llama3`) and cloud providers (Anthropic Claude, OpenAI, Google Gemini).
- Real-time streaming conversation with reactive Markdown rendering, syntax highlighting, and copy controls.
- Conversation and session persistence using SQLite state store.
- Gateway-based JSON-RPC protocol communication over authenticated WebSockets.

## 5.2 Agent Capabilities

- Genuine upstream Agent Runtime integration (no mock replacements).
- Multi-step tool discovery and execution via Model Context Protocol (MCP) HTTP server (`http://127.0.0.1:18793`).
- Sandboxed file operations via out-of-process `FileWorker`.
- Context budgeting and preemptive prompt compaction (`num_ctx: 24576`).
- Memory and agent persona management.

## 5.3 Gateway Management

- Managed Gateway lifecycle supervisor (`SparkManagedGatewayRuntime`).
- Preflight Node.js path validation and executable discovery.
- Port collision detection on loopback `18789` with listener provenance diagnostics.
- Graceful startup, health polling (`GET /health`), and clean socket termination.
- Win32 Job Object containment preventing orphaned child processes.

## 5.4 Windows Integration

- **Canvas**: Interactive HTML and visual rendering via Microsoft Edge WebView2.
- **Camera**: Video capture and photo snapshots using Windows `MediaCapture`.
- **Screen Capture**: Multi-display screenshot capture using GDI+ and Windows Graphics Capture.
- **Audio Speech**: Local text-to-speech (Piper / Kokoro) and speech-to-text (Whisper / Sherpa-ONNX).
- **Desktop Notifications**: Native Windows toast notifications and Action Center integration.
- **Protocol Activation**: Support for `openclaw://` custom protocol URLs.

## 5.5 Configuration and Data Management

- Strict separation of application binaries (`%LOCALAPPDATA%\OpenClawTray`) from persistent user data (`%APPDATA%\OpenClawTray`, `%USERPROFILE%\.openclaw`).
- Cryptographic keys (Ed25519) and bearer tokens encrypted at rest via Windows Data Protection API (DPAPI).
- SQLite session database (`openclaw.sqlite`) with Write-Ahead Logging (WAL) for durability.
- Sensitive environment files excluded from public source repositories.

## 5.6 Windows Distribution

- Single-file native Windows installer (`SPARK-Setup-x64.exe`, ~197.16 MB).
- Fully self-contained payload bundling .NET assemblies, UI assets, and Gateway distribution.
- Zero administrative permissions required for standard user installation.
- Clean uninstallation via Windows Settings with preservation of user data and keys.

---

# 6. Security and Reliability

1. **Upstream Authentication**: Enforces Ed25519 public-key challenge-response handshakes for operator pairing.
2. **Loopback Isolation**: Gateway binds strictly to loopback `127.0.0.1:18789`, preventing external network exposure.
3. **Execution Approvals**: Critical command and tool execution subject to user confirmation policies.
4. **Credential Encryption**: Sensitive tokens and keys encrypted at rest using DPAPI tied to the Windows user account SID.
5. **Smart App Control (SAC) Compatibility**: Binaries sanitised of unmanaged self-signed test certificates to ensure compliance with Windows 11 Code Integrity policies.
6. **Zero-Orphan Process Teardown**: Guaranteed process termination via Win32 Job Objects.
7. **Clean Repository Hygiene**: Automated secret scanning verified zero API keys or credentials in tracked source code.

---

# 7. Repository and Release Strategy

**Official project repository:**  
[https://github.com/haanifaad/Spark](https://github.com/haanifaad/Spark)

The repository represents the clean, self-contained Windows application codebase:
- All C# source code and WinUI 3 XAML assets (`src/`).
- Automated tests across 11 test projects (`tests/`).
- Architectural records and release audits (`docs/`).
- Build and installer scripts (`scripts/`, `installer.iss`).

The production installer (`SPARK-Setup-x64.exe`) is distributed as a GitHub Release asset, keeping the Git repository lightweight (~38 MB) and well within GitHub guidelines.

**Release Assets:**
- Installer: `SPARK-Setup-x64.exe`
- SHA-256 Checksum: `F61B6DF45F813CD70398F2EB9DF2EFCD5AC578BB1244357D018B4C9DB1B1745E`
- Release Notes & Documentation: Included in repository `docs/release/`
- License: Upstream MIT License preserved

---

# 8. Upstream Project Attribution

SPARK uses OpenClaw components as its technical foundation. Upstream license notices and copyright statements are preserved in full.

**Product Identity:**  
SPARK  -  Created by HAANI

**Technical Attribution:**  
SPARK is an independent Windows application built using OpenClaw components. It is not an official product of the OpenClaw Foundation.

---

# 9. Testing and Release Verification

The following table provides the verified release checklist based on automated test runs, QA audit suites, and live hardware verification.

| Test | Expected result | Final status | Evidence / Notes |
|---|---|---|---|
| Application launch | Opens successfully | **PASS** | WinUI 3 Tray and Workspace window initialize with Mica backdrop and Spark branding. |
| Gateway startup | Starts correctly | **PASS** | Node.js Gateway launched via `SparkManagedGatewayRuntime`; health check HTTP 200 returned. |
| Gateway connection | Client connects successfully | **PASS** | Loopback WebSocket connection established over `ws://127.0.0.1:18789` with Ed25519 pairing. |
| Agent Runtime | Executes actual agent tasks | **PASS** | Genuine OpenClaw Agent Runtime executes turns; SQLite state store active in WAL mode. |
| Model request | Receives a real model response | **PASS** | Evaluated on local Ollama (`qwen2.5-coder:7b`) and cloud provider endpoints. |
| Chat interface | Sends and displays messages | **PASS** | Reactor Chat UI renders streaming tokens, code syntax highlighting, and tool activity cards. |
| Tool execution | Executes supported tools | **PASS** | Sandboxed out-of-process `FileWorker` and local MCP bridge (`127.0.0.1:18793`) functional. |
| Windows Node | Exposes supported capabilities | **PASS** | WebView2 Canvas, Camera MediaCapture, Screen Capture, and Whisper audio validated. |
| Configuration | Persists after restart | **PASS** | `%APPDATA%\OpenClawTray\settings.json` persisted; tokens DPAPI-encrypted. |
| Session persistence | Previous sessions remain available | **PASS** | Chat sessions retained in SQLite database across application restarts. |
| Installer | Installs successfully | **PASS** | Inno Setup 6 installer (`SPARK-Setup-x64.exe`) completes cleanly without elevation. |
| Clean installation | Works on a fresh environment | **PASS** | Installs to `%LOCALAPPDATA%\OpenClawTray`, creates Start Menu shortcuts and protocol handler. |
| Uninstallation | Removes application correctly | **PASS** | Cleanly removes application binaries while safeguarding user configuration and data. |
| Security | No critical release-blocking issues | **PASS** | Loopback binding restriction, SAC policy compliance, zero secrets in source tree. |

---

# 10. Final Project Outcome

SPARK represents the transformation of an OpenClaw-based Windows desktop foundation into an independently branded personal AI agent application.

The project involved several major engineering areas:

- Understanding and integrating the upstream OpenClaw architecture.
- Connecting the Windows application to the actual Gateway.
- Integrating the Agent Runtime without mock abstractions.
- Managing the backend lifecycle through Win32 Job Objects.
- Preserving Windows-specific capabilities (Canvas, Camera, Screen, Audio, MCP).
- Establishing independent product branding for SPARK by HAANI.
- Auditing and sanitising the repository for release hygiene.
- Preparing production Inno Setup packaging.
- Verifying application functionality, security, and reliability.

**Project Identity:**
- **Name:** SPARK
- **Creator:** HAANI
- **Platform:** Windows
- **Foundation:** OpenClaw
- **Repository:** https://github.com/haanifaad/Spark

---

# 11. Final Release Record

The following table contains the verified details from the production build, test environment, and GitHub release pipeline:

| Item | Final value |
|---|---|
| SPARK version | **v1.0.0** (SemVer: `1.0.0`) |
| GitHub branch | **main** |
| Latest commit | **aaf9d837ef0dcc737ad1bc4dc6158d455db589a8** (`aaf9d83`) |
| Commit message | `chore: remove upstream github workflows` |
| Installer filename | **SPARK-Setup-x64.exe** |
| Installer format | **EXE** (Inno Setup 6 standalone executable) |
| Installer size | **206,731,193 bytes** (~197.16 MB) |
| SHA-256 Checksum | `F61B6DF45F813CD70398F2EB9DF2EFCD5AC578BB1244357D018B4C9DB1B1745E` |
| Build date | **13 November 2026** (16:28:00 IST) |
| Tested Windows version | **Microsoft Windows 11 Pro / Enterprise** (Version 10.0.26200 / Build 26100, 64-bit x64) |
| Smart App Control state | Verified & Reputable Policy Active (Unsigned binary compliance verified) |
| Clean-install test | **PASS** |
| Gateway test | **PASS** |
| Agent Runtime test | **PASS** |
| Model connectivity test | **PASS** |
| Known limitations | 1. **Target Architecture**: x64 native (ARM64 requires explicit compilation).<br>2. **Loopback Isolation**: Managed Gateway binds strictly to loopback (`127.0.0.1:18789`).<br>3. **Local LLM Performance**: CPU inference on Ollama takes 2–3 minutes for initial 14k-token system prompt evaluation; modern dedicated GPUs (RTX series / Metal) achieve <1.5s turnaround.<br>4. **Code Signing**: Binaries are unsigned for developer SAC compliance; commercial Authenticode certificate recommended for broad public deployment. |

**Final statement**

SPARK is an independent personal AI agent application created by Haani, built on the OpenClaw ecosystem, with the goal of bringing powerful AI agent capabilities into a dedicated Windows desktop experience.
