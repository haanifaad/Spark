# SPARK Autonomous Personal AI Agent -- Architectural Audit & Systems Inventory

**Document Version:** 1.0.0  
**Target Date:** 2026-11-19  
**Execution Environment:** Windows x64 (.NET 10.0 / WinUI 3 / Node.js 22 LTS)  
**Root Repository:** `C:\Intel\Spark\Spark-main`  
**Status:** Complete Architectural Audit (Phase A Milestone Deliverable)

---

## 1. Executive Summary

SPARK is a high-performance Windows desktop AI environment combining a native WinUI 3 frontend with a supervised OpenClaw Gateway runtime, embedded C++ and ONNX runtimes (Whisper, Kokoro, Piper, Silero VAD, CUDA/Llama), a local Node sandbox (MXC), and an extensible capability bus (`INodeCapability`).

The primary directive is to extend SPARK into a fully autonomous personal AI agent capable of proactive heartbeat execution, long-term personalized memory, multi-channel messaging (Telegram, WhatsApp), unified tool orchestration with human-in-the-loop safety gating, intelligent capability-aware multi-model routing, and bilingual (English and Malayalam) persona configuration.

This audit evaluates the exact state of all existing subsystems in `C:\Intel\Spark\Spark-main`, catalogues proven foundations to reuse, identifies architectural gaps, and specifies the roadmap across Phases A through J.

---

## 2. Solution & Project Topology

| Project | Target Framework | Core Responsibility & Architectural Role | Status |
| :--- | :--- | :--- | :--- |
| `OpenClaw.Shared` | `net10.0` | Common models, protocols, `INodeCapability`, Cron models, MCP bridge, Exec approvals, MXC Sandbox, Audio/STT/TTS, Inference catalog, Slash commands. | Fully operational; primary engine library |
| `OpenClaw.Chat` | `net10.0` | Timeline reduction (`ChatTimelineReducer`), immutable message state, model choices, thinking levels, permission request timeline items. | Operational; recently extended with message editing and branching |
| `OpenClaw.Connection` | `net10.0` | WebSocket gateway client (`OpenClawGatewayClient`), process host (`SparkManagedGatewayRuntime`), device pairing, credential resolution. | Operational; robust supervisor with Win32 Job Object isolation |
| `OpenClaw.Tray.WinUI` | `net10.0-windows10.0.19041.0` | WinUI 3 desktop UI, navigation pages, settings, automations editor, chat composer, system tray coordinator. | Operational; rich desktop interface |
| `OpenClaw.FileWorker` | `net10.0` | Background file transfer engine, frame streaming, upload retention. | Operational |
| `OpenClaw.Cli` & `WinNode.Cli` | `net10.0` | Command line operator interfaces for node management and diagnostics. | Operational |
| `OpenClaw.Shared.Tests` | `net10.0` | Unit & integration test suite (over 3,000 tests covering protocol, sandbox, approvals, capabilities). | Operational (requires code signing for WDAC/AppLocker compliance) |

---

## 3. Subsystem Audit & Inventory

### 3.1 Gateway Architecture & Protocol
- **Location:** `src/OpenClaw.Connection/NativeGateway/SparkManagedGatewayRuntime.cs`, `src/OpenClaw.Connection/GatewayConnectionManager.cs`
- **What Works:**
  - Node.js OpenClaw gateway runtimes are supervised through Windows Win32 Job Objects (`KILL_ON_JOB_CLOSE`), preventing orphaned background processes.
  - Bidirectional JSON-RPC over WebSocket handles chat streams, agent lifecycle events (`stream=lifecycle`, `stream=assistant`, `stream=reasoning`, `stream=tool`), device pairing, and session persistence.
  - Automatic port resolution and credential resolution (`DeviceIdentityStore`).
- **Gaps & Requirements:**
  - High-level autonomous agent lifecycle needs a unified background loop decoupled from whether a chat window is actively open.
  - Gateway message hooks must support bidirectional bridge to external channels.

### 3.2 Chat Runtime & Message Timeline
- **Location:** `src/OpenClaw.Chat/ChatTimelineReducer.cs`, `src/OpenClaw.Tray.WinUI/Chat/OpenClawChatDataProvider.cs`
- **What Works:**
  - Immutable state management via `ChatTimelineState` and `ChatTimelineReducer`.
  - Supports tool calls, inline execution approvals (`ChatPermissionDecision`), streaming reasoning tokens, diff statistics, and multi-turn turns.
  - Recent extensions support editing past user prompts, regenerating alternative responses, and token context budgeting.
- **Gaps & Requirements:**
  - Integration with long-term memory retrieval: prompts need automatic contextual memory injection.
  - Model routing recommendations should dynamically suggest optimal models based on prompt complexity.

### 3.3 Model Inference & Capability Matrix
- **Location:** `src/OpenClaw.Shared/Inference/ModelCapability.cs`, `ChatModelPreferencesStore.cs`, `LocalModelCatalog.cs`
- **What Works:**
  - `ModelCapabilityMatrix` identifies flags: `General`, `Reasoning`, `Coding`, `Mathematics`, `Vision`, `Multimodal`, `ToolCalling`, `FastResponse`, `LongContext`, `LocalInference`.
  - `ChatModelPreferencesStore` persists favorite and recently used models in JSON format.
  - Local model runtime support for Ollama, GGUF/llama.cpp, and NVIDIA CUDA driver probing.
- **Gaps & Requirements:**
  - Needs a dynamic **Intelligence Model Router** that chooses between local and cloud providers based on task categorization, cost, latency, token context size, and strict privacy boundaries (preventing confidential data from leaking to cloud models).

### 3.4 Memory & Personalization (Foundations vs Incomplete)
- **Location:** `src/OpenClaw.Shared/Commands/SparkIntelligenceModels.cs` (Lines 8–30)
- **What Works:**
  - Initial contract definitions: `SparkMemoryCategory` (Preference, Project, Learning, Technical, Goal, Decision) and `SparkMemoryEntry`.
  - `/memory` slash command registered in `SparkSlashCommandRegistry.cs`.
- **Incomplete / Missing:**
  - **Memory Storage Engine:** No persistent semantic memory repository with indexing, vector/BM25 relevance ranking, or SQLite/JSON persistence.
  - **Memory Partitioning:** Need clear architectural separation between:
    1. User Profile (facts, preferences, language).
    2. Long-term Semantic Memory (facts, concepts).
    3. Episodic Conversation Memory (historical highlights).
    4. Project Memory (codebase rules, active project constraints).
    5. Active Session Context & Temporary Working Memory.
  - **Persona Engine:** Configurable agent personality, communication styles, explicit behavioral boundaries, and **bilingual support (English & Malayalam)**.
  - **User Memory Controls:** Inspect, update, correct, export, and delete memories with full provenance and privacy controls.

### 3.5 Cron & Automations (Scheduler & Heartbeat)
- **Location:** `src/OpenClaw.Shared/Cron/AutomationsStore.cs`, `CronModels.cs`, `src/OpenClaw.Tray.WinUI/Pages/CronPage.xaml`
- **What Works:**
  - Full CRUD integration with Gateway Cron RPC (`cron.list`, `cron.create`, `cron.update`, `cron.remove`, `cron.run`, `cron.status`).
  - Supports `At` (one-shot), `Every` (interval), `Cron` (calendar expression), and MCP event triggers.
  - WinUI management interface on `CronPage.xaml` for scheduling and monitoring jobs.
- **Gaps & Requirements:**
  - Background autonomous heartbeat checks for agent self-initiated tasks (email/inbox monitoring, daily summaries, task reminders, project health checks).
  - Explicit bounding of background execution: bounded timeout, retry policies with exponential backoff, failure reporting, and execution audit history.

### 3.6 MCP, Skills & Tool Registry
- **Location:** `src/OpenClaw.Shared/Mcp/McpToolBridge.cs`, `src/OpenClaw.Shared/Capabilities/`
- **What Works:**
  - Transport-agnostic MCP server exposing all registered `INodeCapability` instances to MCP clients via JSON-RPC.
  - Existing capabilities: `BrowserProxyCapability`, `FileCapability`, `CameraCapability`, `CanvasCapability`, `DeviceCapability`, `LocationCapability`, `OllamaCapability`, `ScreenCapability`, `SttCapability`, `TtsCapability`, `SystemCapability`.
- **Gaps & Requirements:**
  - Needs a **Unified Tool Registry** classifying tools by risk level (`Safe`, `Low`, `Medium`, `High`, `Critical`), input/output schema, execution timeout, cancellation tokens, and required human approvals.
  - Specialized Development tools (Git status/commit/diff, repo inspection, build/test runners).
  - Standardized external service connectors (Google Workspace, Calendar, Email) with clear status indicators for configured vs unconfigured accounts.

### 3.7 Human-in-the-Loop Security & Exec Approvals
- **Location:** `src/OpenClaw.Shared/ExecApprovals/`
- **What Works:**
  - Enterprise-grade command validation, path normalization, and allowlist matching (`ExecApprovalsCoordinator`, `ExecApprovalEvaluator`).
  - Strict secret redaction in `ExecApprovalSecretRedactor.cs` (1,200+ lines covering API tokens, AWS keys, passwords, bearer headers, query params).
  - Inline chat permission request cards (`ChatTimelineItemKind.PermissionRequest`) and WinUI approval dialogs (`ExecApprovalV2DialogPromptHandler`).
- **Gaps & Requirements:**
  - Generalize approval infrastructure beyond shell execution to high-impact autonomous actions: sending external messages (Telegram/WhatsApp), file deletions, privileged API calls, financial/sensitive operations.
  - Guarantee that operation approvals are cryptographically or token-bound to the exact payload (re-approval required if payload changes). LLM must never approve its own operations.

### 3.8 Sandboxing & Audit Infrastructure
- **Location:** `src/OpenClaw.Shared/Mxc/NodeSandboxPolicy.cs`, `NodeSandboxExecutor.cs`, `DiagnosticsJsonlService.cs`
- **What Works:**
  - `NodeSandboxPolicy` enforces filesystem boundaries, environment variable scrubbing, and path access checks.
  - Append-only structured JSONL logging with background writer channels and rotation in `DiagnosticsJsonlService.cs`.
- **Gaps & Requirements:**
  - Centralized **Autonomous Agent Audit Logger** specifically capturing: timestamp, user/session, agent, tool, operation, risk classification, approval status, execution outcome, execution duration, and redacted parameters.

### 3.9 Messaging Channels
- **Location:** `src/OpenClaw.Shared/Models.cs` (`ChannelHealth`, `ChannelCommandCenterInfo`), `ConnectionPageChannelMetrics.cs`
- **What Works:**
  - Data models for tracking channel health and statuses reported by the gateway.
- **Incomplete / Missing:**
  - Native channel connectors for **Telegram Bot API** and **WhatsApp Cloud API** (official/authorized).
  - Channel message router: bidirectional mapping between external chat messages and SPARK sessions.
  - Media/attachment handling (voice notes, images, documents) over messaging channels.
  - Secure credential storage (Windows DPAPI / Credential Manager) for channel tokens.

### 3.10 Voice & Media Pipeline
- **Location:** `src/OpenClaw.Shared/Audio/SpeechToTextService.cs`, `WhisperModelManager.cs`, `KokoroModelManager.cs`, `PiperVoiceManager.cs`, `VoiceService.cs`
- **What Works:**
  - Speech-to-text with local Whisper models (`ggml-whisper`).
  - Text-to-speech with Kokoro and Piper neural voice models.
  - Silero VAD (Voice Activity Detection).
  - Audio capture and playback pipeline in `VoiceService.cs`.
- **Gaps & Requirements:**
  - Direct voice-to-agent task execution: transcribing user voice commands into autonomous agent goals.
  - Media input pipeline for analyzing attached images and documents alongside agent queries.

### 3.11 User Interface (WinUI 3)
- **Location:** `src/OpenClaw.Tray.WinUI/Pages/`
- **What Works:**
  - Modern WinUI 3 pages: `ChatPage`, `CronPage`, `PermissionsPage`, `SandboxPage`, `SettingsPage`, `LocalAiPage`, `VoiceSettingsPage`, `UsagePage`, `DashboardsPage`.
- **Gaps & Requirements:**
  - Management views for Memory (view, search, delete, correct memories).
  - Persona Settings (agent identity, tone, bilingual language preferences, behavioral constraints).
  - Messaging Channels configuration panel (Telegram bot token, WhatsApp API setup, connection statuses).
  - Unified Tools & Skills browser.
  - Audit log viewer.

### 3.12 Autonomous Agent Core & 14-State Task Engine
- **Location:** Needs central architecture in `src/OpenClaw.Shared/Orchestration/`
- **Gaps & Master Requirements:**
  - 14-state persistent task lifecycle: `Received`, `Interpreting`, `Planning`, `WaitingForApproval`, `Ready`, `Executing`, `WaitingForExternalInput`, `Paused`, `Reviewing`, `Verifying`, `Completed`, `Failed`, `Cancelled`, `RequiresUserIntervention`.
  - Task decomposition: decomposes high-level user instructions (English/Malayalam) into multi-step DAGs with tool bindings and dependency resolution.
  - Step telemetry: Step ID, description, tool used, input/output refs, risk classification, approval requirement, retry count, verification result, error info.
  - Atomic state transitions with crash recovery after application/worker restart.

### 3.13 Sentinel -- Independent Security Reviewer
- **Location:** Needs independent component in `src/OpenClaw.Shared/Security/Sentinel/`
- **Gaps & Master Requirements:**
  - Independent second-order review of all high-risk proposals before execution.
  - Does NOT repeat primary agent reasoning; deterministically inspects user authorization, tool permissions, target destination, recipient, financial amounts, data transmitted, destructive effects, privacy implications, policy violations, and scope drift.
  - Returns structured `SentinelReviewResult`: `Approved`, `Rejected`, `RequiresUserApproval`, `RequiresMoreInformation`.
  - Enforces that primary agent CANNOT override a Sentinel rejection.

### 3.14 Specialist Agents & India-First Financial Safeguards
- **Location:** Needs specialist subsystems in `src/OpenClaw.Shared/Specialists/`
- **Gaps & Master Requirements:**
  - **Email Agent:** Gmail & Outlook connector integration, search, categorize, identify unanswered, summarize, draft, approval-based sending with full preview.
  - **Travel & Booking Agent:** Flights, trains, buses, hotels; India-focused provider workflows (IRCTC, MakeMyTrip, RedBus patterns); verification of confirmation references; zero simulated bookings.
  - **Forms Agent:** Form field extraction, shared memory population, validation, preview, approval-gated submission.
  - **Shopping Agent & Spending Limits:** Product research, merchant comparison, cart preparation, hosted checkout (Stripe Link, Shop Pay, UPI-compatible providers).
  - **Spending Limits Engine:** Enforces per-transaction, daily, monthly, per-merchant, and category-based spending limits in the backend. Strictly blocks autonomous bank transfers, UPI PIN entry, card authentication.

### 3.15 Secure Browser Automation & User Takeover
- **Location:** Needs browser management in `src/OpenClaw.Shared/Browser/`
- **Gaps & Master Requirements:**
  - Live browser activity status (`Active`, `Navigating`, `Interacting`, `WaitingForUser`, `Paused`, `UserTakeover`).
  - Seamless user takeover: user can pause the agent, take manual control of the browser session, and safely return control to the agent.
  - Secret redaction preventing passwords, session tokens, or payment credentials from appearing in screenshots or model context.

### 3.16 Reversal, Undo & Result Verification
- **Location:** Needs verification engine in `src/OpenClaw.Shared/Verification/`
- **Gaps & Master Requirements:**
  - Reversibility declarations: `Reversible`, `PartiallyReversible`, `Irreversible`, `ReversibleOnlyThroughProviderWorkflow`.
  - Idempotency keys preventing duplicate bookings, orders, or emails.
  - Post-execution verification: checks provider message ID, booking reference, transaction status, file existence/validity, build/test validation. Never marks Completed based on unverified LLM claims alone.
  - Confidence & Hallucination Control: categorizes info as `Verified`, `ProviderReturned`, `SourceSupported`, `ModelInference`, `UnverifiedAssumption`. Displays confidence (`High`, `Medium`, `Low`) with source citations.

---

## 4. Master Architectural Roadmap (Phases 0 through 11)

| Phase | Milestone | Priority | Focus Area | Status |
| :--- | :--- | :--- | :--- | :---: |
| **Phase 0** | Repository Audit & Architecture | **DONE** | Audit existing capabilities, map gaps, establish phase tracker. | **COMPLETED** |
| **Phase 1** | Autonomous Agent Core & Task Engine | **HIGH** | `AgentOrchestrator`, `TaskPlanner`, `TaskDecomposer`, `TaskStateManager`, 14-state task model. | **IN PROGRESS** |
| **Phase 2** | Shared Memory & Bilingual Persona | **DONE** | 6-tier scoped memory store, BM25 semantic retrieval, Malayalam/English persona engine. | **COMPLETED** |
| **Phase 3** | Sentinel Security Reviewer & Approvals | **HIGH** | Independent Sentinel reviewer, risk evaluator, cryptographic SHA-256 operation binding. | **NEXT** |
| **Phase 4** | Modular Tool & Connector Framework | **HIGH** | Reversible tool declarations, sandbox validation, connector contracts. | **PENDING** |
| **Phase 5** | Multi-Model Router & Providers | **DONE** | 9-category capability router, cost/latency scoring, strict local privacy isolation. | **COMPLETED** |
| **Phase 6** | Secure Browser & User Takeover | **HIGH** | Browser worker, live activity view, pause/resume, seamless user takeover. | **PENDING** |
| **Phase 7** | Email Agent & Communication | **MEDIUM** | Gmail/Outlook connectors, search, summarize, draft, approval-gated sending. | **PENDING** |
| **Phase 8** | Travel, Shopping & Spending Limits | **HIGH** | Travel agent, forms agent, shopping agent, backend spending limits engine. | **PENDING** |
| **Phase 9** | Background Autonomy & Worker | **MEDIUM** | Background task runner, heartbeat, morning briefings, restart recovery. | **PENDING** |
| **Phase 10** | Multimodal Voice & Specialists | **DONE** | Whisper STT, WAV TTS, vision, document analysis, voice task orchestration. | **COMPLETED** |
| **Phase 11** | Verification, Reversal & Tasks UI | **HIGH** | Result verifier, reversal manager, confidence/sources, WinUI Tasks Dashboard. | **PENDING** |

---
*Generated autonomously by SPARK Architecture & Implementation Engine.*
