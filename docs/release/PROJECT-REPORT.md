# SPARK
## Project Development, Architecture & Release Report

**Project:** SPARK — Personal AI Agent for Windows  
**Creator:** HAANI  
**Upstream Technology:** OpenClaw  
**Platform:** Microsoft Windows  
**Repository:** https://github.com/haanifaad/Spark  
**Report Date:** 17 November 2026  

---

# 1. Executive Summary

SPARK is a Windows-native personal AI agent application developed by Haani, inspired by the capabilities of JARVIS and modern AI agent systems.

The project began with the goal of turning the existing OpenClaw ecosystem into a more accessible, integrated Windows desktop experience.

Rather than building an entirely new AI agent framework from scratch, the project uses OpenClaw as its underlying technology and combines it with a Windows desktop application.

The objective was to create a unified application through which users can interact with AI models, manage agent sessions, use tools, connect capabilities, and access an AI assistant through a native Windows interface.

The project also involved replacing the user-facing OpenClaw branding with SPARK, auditing the codebase, removing unnecessary repository content where safe, preparing a production build, and creating a Windows installer.

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

The application manages the backend and presents its capabilities through a unified desktop experience.

# 3. Development Journey — What Was Done

## Phase 1 — Understanding OpenClaw

The first major task was understanding the upstream OpenClaw ecosystem.

The project required distinguishing between three important components:

- **Gateway:** Handles communication and coordination between clients and the agent system.
- **Agent Runtime:** Executes agent tasks and manages the underlying agent workflow.
- **Windows Node:** Exposes Windows-specific capabilities to the Gateway.

An important architectural decision was made: the Windows desktop client alone was not sufficient to provide the complete OpenClaw experience. The actual Gateway and Agent Runtime also needed to be integrated.

## Phase 2 — Windows Desktop Foundation

The Windows application was based on the existing OpenClaw Windows Node project.

The foundation provides the Windows desktop integration layer, including its existing application infrastructure and supported Windows capabilities.

The project retained the existing architecture instead of unnecessarily rewriting the entire application.

## Phase 3 — Gateway Integration

The project focused on integrating the actual OpenClaw Gateway.

The integration includes:

- Gateway startup and shutdown management.
- Gateway connection handling.
- Runtime monitoring.
- Health checks via HTTP `/health`.
- Logging and diagnostics.
- Restart and recovery management.
- Prevention of duplicate or orphaned Gateway processes via Win32 Job Objects (`KILL_ON_JOB_CLOSE`).

The Gateway remains in its supported Node.js/TypeScript environment rather than being rewritten simply to fit the Windows application.

## Phase 4 — Agent Runtime Integration

A Gateway connection alone does not make a complete AI agent.

SPARK therefore integrated access to the actual Agent Runtime and its associated capabilities.

The integration scope includes:

- Agent execution via `chat.send` and `chat.stream`.
- Model-provider connectivity (local Ollama and cloud providers).
- Session management.
- Tool execution via `node.invoke.request` and `node.invoke.result`.
- Memory functionality.
- Skills and integrations.
- Agent communication through the Gateway.

## Phase 5 — SPARK Branding & Five Core Divisions

The application was rebranded from its OpenClaw-facing identity to SPARK:

**Product:** SPARK  
**Creator:** HAANI  
**Platform:** Windows

The branding covers user-facing application identity:
- Application name and tray UI.
- Desktop presentation and Windows 11 Dark Desktop palette styling (`#111214`, `#1A1B1E`, `#202124`, `#27282B`, accent `#FFF97316`).
- Installer identity and shortcuts.
- Application metadata and manifests.
- Documentation and user-facing references.

Architecturally, SPARK structures workflows into **Five Core Divisions**:
1. **Chat:** Standard conversational AI workspace for interactive reasoning and execution.
2. **Work:** Productivity workspace housing Workboard, Worktrees, Automations, Dashboards, and Sessions.
3. **Code:** Dedicated development workspace architected for OpenCode and Hermes Agent integration.
4. **Study:** Dedicated learning, research, reading, and knowledge synthesis workspace.
5. **Creative:** Dedicated generative studio and creative application workspace.

A critical distinction was maintained: SPARK is an independent application built using OpenClaw components, not an official OpenClaw product. Upstream attribution, licensing, package identifiers, and technical compatibility identifiers remain intact.

## Phase 6 — Repository Audit and Cleanup

The GitHub repository was reviewed to eliminate unnecessary files and folders:

- **Clean Git Footprint:** 39.33 MB (well below GitHub's limits).
- **Separation of Workspaces:** Workstation development files (Node monorepo, pnpm cache, intermediate obj/bin) are isolated and excluded via `.gitignore`.
- **Security Check:** Zero API keys, private certificates, or user credentials committed.

## Phase 7 — Production Build and Installer

The project delivered a distributable Windows application:

- **Installer Output:** Inno Setup 6 Native Windows Installer (`SPARK-Setup-x64.exe`).
- **File Size:** 206,729,872 bytes (197.15 MB).
- **Self-Contained Deployment:** Contains the tray host, WinUI 3 shell, FileWorker, WebView2 runtime integration, and localization assets.
- **Code Signing:** Authenticode signed using developer certificate `CN=SparkDev`.

## Phase 8 — Verification and Bug Fixing

The verification checked the complete end-to-end pipeline:
- Application startup and graceful shutdown.
- Gateway lifecycle supervision and loopback binding (`127.0.0.1:18789`).
- Smart App Control (SAC) policy resolution (0x800711C7) and Authenticode signing.
- Live model execution via Ollama (`qwen2.5-coder:7b`) with expanded context window (`24576`).
- Clean installation and uninstallation verification.

# 4. Technical Architecture

## 4.1 High-Level Architecture

```text
                  SPARK
             Windows Desktop App
                      |
          Application / UI Layer
     (WinUI 3 - Windows 11 Dark Desktop)
                      |
             Gateway Client
                      |
          Gateway Lifecycle Manager
        (Win32 Job Object Supervisor)
                      |
             OpenClaw Gateway
          (Node.js / Port 18789)
                      |
              Agent Runtime
                      |
       +--------------+--------------+
       |              |              |
   AI Models       Tools          Memory
 (Ollama/Cloud) (Native/MCP)   (Sessions/SQLite)
       |              |              |
   Providers      Integrations    Sessions
                      |
               Windows Node
                      |
       Windows-Specific Capabilities
     (Canvas, Camera, Screen, Audio)
```

## 4.2 Technology Stack

| Component | Technology / Foundation |
|---|---|
| Desktop foundation | OpenClaw Windows Node |
| Desktop application | Windows App SDK 2.6.5 / WinUI 3 / .NET 10.0 |
| UI Design System | Windows 11 Dark Desktop Palette (`#111214`, `#1A1B1E`, `#FFF97316`) |
| Backend Gateway | OpenClaw Gateway (v2026.11.3) |
| Gateway runtime | Node.js (v26.13.0) / TypeScript |
| Agent infrastructure | OpenClaw Agent Runtime |
| Communication | Loopback JSON-RPC & WebSocket (`ws://127.0.0.1:18789`) |
| Platform | Microsoft Windows 10 / 11 (x64 native) |
| Source control | Git & GitHub (`https://github.com/haanifaad/Spark`) |
| Distribution | Inno Setup 6 Native Installer (`SPARK-Setup-x64.exe`) |

# 5. SPARK Features

## 5.1 Core AI Assistant
- Desktop-based AI assistant with Mica backdrop and system tray integration.
- AI model connectivity through Ollama (`qwen2.5-coder:7b`) and supported cloud providers.
- Real-time streaming response via `chat.stream`.
- Multi-session management and conversation persistence.

## 5.2 Agent Capabilities
- Full Agent Runtime integration.
- Tool invocation protocol (`node.invoke.request` / `node.invoke.result`).
- Long-term memory support backed by embedded SQLite.
- MCP bridge compatibility for external agent tool extensions.

## 5.3 Gateway Management
- Automated Gateway lifecycle managed directly by the C# desktop host.
- Preflight Node.js runtime validation and port availability checks.
- Child process lifecycle bound to Win32 Job Object with `KILL_ON_JOB_CLOSE`.
- HTTP health polling on `/health` with timeout and auto-recovery.

## 5.4 Windows Integration
- Desktop notifications and system tray action menu.
- Canvas integration with WebView2.
- Screen capture via GDI+ and Windows Graphics Capture.
- Camera frame acquisition via Windows MediaCapture.
- Audio synthesis and speech recognition integration.

## 5.5 Configuration and Data Management
- Dedicated user state separation under `%APPDATA%\OpenClawTray` and `%LOCALAPPDATA%\OpenClawTray`.
- Cryptographic secret protection utilizing Windows DPAPI (`CryptProtectData`).
- Ed25519 cryptographic key generation and challenge-response authentication.

## 5.6 Windows Distribution
- Self-contained production installation via `SPARK-Setup-x64.exe`.
- Start menu and desktop shortcut creation.
- Clean uninstallation and user data preservation options.

# 6. Security and Reliability

1. **Strict Loopback Binding:** Gateway binds exclusively to `127.0.0.1` (`--bind loopback`), preventing exposure to external networks.
2. **Mutual Cryptographic Pairing:** Ed25519 cryptographic keypair handshake required for WebSocket connections.
3. **Data Protection API (DPAPI):** Tokens, passwords, and model API keys are stored encrypted via Windows DPAPI.
4. **Smart App Control (SAC) Compliance:** Assemblies and installer signed with Authenticode developer certificate.
5. **Zero-Orphan Process Guarantee:** Win32 Job Object containment ensures all background processes terminate when the application closes.
6. **No Secret Commits:** Git repositories audited with 0 secrets or private credentials present.

# 7. Repository and Release Strategy

**Official Project Repository:**  
https://github.com/haanifaad/Spark

- **Repository Role:** Development, source control, and automated issue tracking.
- **Installer Deliverable:** Distributed via GitHub Releases (`v1.0.0`), keeping the main source repository lightweight.
- **Release Assets:**
  - `SPARK-Setup-x64.exe` (Standalone Windows Installer)
  - Release verification notes and architectural reports.
  - Verified SHA-256 checksums.

# 8. Upstream Project Attribution

SPARK uses OpenClaw components as its technical foundation. Upstream MIT license notices and copyright statements are preserved.

**Product Attribution:**  
**SPARK — Created by HAANI**

**Technical Attribution:**  
**SPARK is an independent Windows application built using OpenClaw components.**

# 9. Testing and Release Verification

| Test | Expected result | Final status |
|---|---|---|
| Application launch | Opens successfully with WinUI 3 tray and Mica window | **Pass** |
| Gateway startup | Starts correctly on loopback port 18789 | **Pass** |
| Gateway connection | Client connects and completes Ed25519 handshake | **Pass** |
| Agent Runtime | Executes actual agent tasks and dispatch pipeline | **Pass** |
| Model request | Receives real model response (Ollama / Cloud) | **Pass** |
| Chat interface | Sends, streams, and displays Markdown messages | **Pass** |
| Tool execution | Executes supported native and MCP tools | **Pass** |
| Windows Node | Exposes Canvas, Camera, Screen, and Audio capabilities | **Pass** |
| Configuration | Persists after restart in `%APPDATA%\OpenClawTray` | **Pass** |
| Session persistence | Previous sessions remain available across app launches | **Pass** |
| Installer | Compiles cleanly via Inno Setup 6 | **Pass** |
| Clean installation | Installs silently via `/VERYSILENT` and extracts payload | **Pass** |
| Uninstallation | Cleanly uninstalls binaries while protecting user data | **Pass** |
| Security | DPAPI encryption, loopback binding, Job Object teardown | **Pass** |

# 10. Final Project Outcome

SPARK transforms an OpenClaw-based Windows desktop foundation into an independently branded, cohesive personal AI agent desktop application.

**Project Identity:**
- **Name:** SPARK
- **Creator:** HAANI
- **Platform:** Windows
- **Foundation:** OpenClaw
- **Repository:** https://github.com/haanifaad/Spark

---

# 11. Final Release Record

| Item | Final value |
|---|---|
| SPARK version | **v1.0.0** |
| GitHub branch | **main** |
| Latest commit | **88b206c8739f884adec644fb1394bf0caedd002c** (`88b206c`) |
| Installer filename | **SPARK-Setup-x64.exe** |
| Installer format | **EXE (Inno Setup 6.7.3 Native Windows Installer)** |
| Installer size | **206,729,872 bytes (197.15 MB)** |
| SHA-256 Checksum | `45117D8F7028E038DDD0001E16F45E340BFEFCDDB901647550DE2D4BD554767C` |
| Build date | **17 November 2026** |
| Tested Windows version | **Microsoft Windows 11 Pro (24H2 / Build 26100, x64)** |
| Clean-install test | **Pass** |
| Gateway test | **Pass** (`127.0.0.1:18789` `/health` -> `{"ok":true,"status":"live"}`) |
| Agent Runtime test | **Pass** (`chat.send` -> `chat.stream`) |
| Model connectivity test | **Pass** (Ollama `qwen2.5-coder:7b` / Cloud API) |
| Known limitations | Local Ollama requires model pre-pulling; strict Smart App Control mode requires installing local developer certificate `CN=SparkDev` into CurrentUser certificate store. |

**Final statement**

SPARK is an independent personal AI agent application created by Haani, built on the OpenClaw ecosystem, bringing powerful AI agent capabilities into a dedicated Windows desktop experience.
