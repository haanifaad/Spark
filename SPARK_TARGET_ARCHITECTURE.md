# SPARK — Target Architecture Specification
**Document Version:** 1.0.0  
**Target Platform:** Windows 11 (x64 / ARM64)  
**Core Frameworks:** .NET 10, C# 13, WinUI 3 (Windows App SDK 1.6+), Node.js Gateway supervisor, Python AI runtime  

---

## 1. High-Level System Architecture

```text
SPARK — NATIVE WINDOWS DESKTOP AI OPERATING ENVIRONMENT
│
├── WinUI 3 Presentation Layer (Fluent Design / Mica Material)
│   ├── Main Navigation Shell (Collapsible Sidebar, WorkspaceWindow)
│   ├── Divisions:
│   │   ├── Chat Division (Multi-Turn Conversational AI, Streaming, Model Selector)
│   │   ├── Work Division (Task Board, Project Workspaces, Email/Docs Integrations)
│   │   ├── Code Division (Repo Indexing, CodingAgentOrchestrator, Diff Review)
│   │   ├── Study Division (Socratic Tutor, Active Document Analysis, Flashcards)
│   │   ├── Creative Division (17 ArtCraft Creative Applications, Canvas, Audio/Video)
│   │   └── Voice Mode (Mark-LVII Blue HUD, Arc Reactor / 3D Face, Malayalam/English)
│   ├── Command Palette (Ctrl + K Global Fast-Launcher)
│   └── Shared WinUI 3 Design System (Tokens, Typography, Controls, Themes)
│
├── Application & Orchestration Services
│   ├── ISparkDivisionCoordinator (Unified cross-division state & navigation)
│   ├── ISparkAgentOrchestrator (Multi-agent decomposition, execution loops, review)
│   ├── ISparkAutonomousScheduler (Persistent background tasks, heartbeat, retry policies)
│   ├── ISentinelReviewer (Security auditing, tool permission boundaries, approval receipts)
│   └── ISparkApprovalManager (HMAC-verified human-in-the-loop approval gates)
│
├── AI Infrastructure & Model Routing
│   ├── ISparkModelRouter (Task-based model dispatching)
│   ├── Local Model Provider (Ollama: qwen2.5-coder, llama3.2, deepseek-r1)
│   ├── Cloud Model Providers (OpenAI, Anthropic, Gemini, OpenRouter)
│   ├── Streaming Response Pipe (Zero-latency token emission)
│   └── Token Usage & Cost Accounting (`UsagePage.xaml`)
│
├── Tool & Automation Subsystems
│   ├── Computer Use Subsystem (UI-TARS 1000-normalized grounding, PyAutoGUI failsafe)
│   ├── Browser Automation Subsystem (Playwright, isolated profile, BrowserSsrfGuard)
│   ├── File & Workspace Subsystem (Safe directory boundaries, change tracking)
│   └── Model Context Protocol (MCP) Bridge (Python SDK, tool schema registry)
│
├── Unified Memory & Knowledge Architecture
│   ├── Short-Term Context (Working window, sliding token window)
│   ├── Episodic Memory (Conversation turns, SQLite relational storage)
│   ├── Semantic Long-Term Memory (Mem0 / LlamaIndex vector retrieval)
│   └── Memory Governance (User inspection, conflict resolution, deletion)
│
└── Managed Gateway & IPC Runtime
    ├── Local Node.js Gateway Supervisor (Process lifecycle, port binding)
    ├── Secure WebSocket / Named Pipe IPC
    └── Windows DPAPI Encrypted Credential Store
```

---

## 2. Key Subsystem Specifications

### 2.1 Unified Memory Subsystem
The unified memory system eliminates fragmentation across C#, Python, and Node.js:
1. **Authoritative Relational Store**: SQLite database managed by the Gateway runtime storing all structured sessions, messages, and task records.
2. **Episodic & Semantic Store**: Vector embedding store indexing user preferences, factual statements, project decisions, and document extracts.
3. **Cross-Division Retrieval**: Any division (Chat, Code, Study, Work, Creative, Voice) queries memory via `ISparkMemoryStore.SearchAsync` with cosine similarity ranking and time-decay weighting.

### 2.2 Security Architecture & Sentinel Reviewer
1. **Least-Privilege Execution**: Tools are partitioned by permission category (`FilesystemRead`, `FilesystemWrite`, `ProcessExecution`, `NetworkAccess`, `ExternalApiAccess`, `BrowserAutomation`, `ComputerControl`).
2. **Approval-Before-Action**: Irreversible actions (file deletion, git commit/push, financial checkout, email dispatch, command execution) generate an `ApprovalRequest` with an HMAC cryptographic nonce.
3. **Automation Failsafes**: `pyautogui.FAILSAFE = True` guarantees that moving the mouse cursor to any screen corner immediately aborts computer-control tasks.
4. **Credential Isolation**: Secrets are protected using Windows Data Protection API (DPAPI) and never injected into model context or printed in audit logs.

### 2.3 Shared WinUI 3 Design System
1. **Color Tokens**: Semantic brushes for surfaces, borders, text, brand accents, and status indicators.
2. **Typography Scale**: Standardized Display, Title, Subtitle, BodyStrong, Body, and Caption styles adhering to Windows 11 Fluent guidelines.
3. **Spacing Scale**: 4px base grid (`Spacing4`, `Spacing8`, `Spacing12`, `Spacing16`, `Spacing24`, `Spacing32`).
4. **Corner Radius Scale**: Standardized radii (`RadiusSmall` 4px, `RadiusMedium` 8px, `RadiusLarge` 12px, `RadiusCircle` 999px).
5. **Global Command Palette**: Instant modal dialog triggered via `Ctrl + K` for workspace navigation, model switching, division hopping, and task execution.
