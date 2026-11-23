# SPARK Target Architecture

## 1. Architectural Principles

SPARK is designed as a unified personal AI operating environment on Windows, combining a high-performance native desktop UI (WinUI 3 / Windows App SDK 2.6.5 on .NET 10) with an isolated, supervised Agent Runtime and Windows Node capability layer.

Core Architecture Guidelines:
1. Native Windows Integration: Seamless Fluent UI, Mica materials, DPAPI credential protection, Win32 Job Objects, and Windows Graphics Capture.
2. Least-Privilege Execution: Hardware and process sandboxing via Microsoft MXC containers and dedicated browser profiles.
3. Zero-Orphan Process Lifetime: Win32 Job Objects with `KILL_ON_JOB_CLOSE` ensuring clean process teardown on application exit.
4. Extensible Capability Model: Unified `INodeCapability` interfaces automatically exposed over local WebSocket and Model Context Protocol (MCP) HTTP/SSE bridges.
5. Verifiable Auditability: Transactional SQLite changelogs and single-use HMAC confirmation tickets for all sensitive operations.

---

## 2. High-Level Subsystem Architecture

```
+-----------------------------------------------------------------------------------+
|                                SPARK Desktop UI                                   |
|   WinUI 3 (Windows App SDK / .NET 10) | Mica | Sessions Hub | Reactor Chat | HUD  |
+-----------------------------------------------------------------------------------+
                                   │
              IPC / Local Loopback │ (Named Pipes / Memory Transport)
                                   ▼
+-----------------------------------------------------------------------------------+
|                             SPARK Agent Runtime                                   |
|  - Agent Lifecycle & Task Queue (Checkpointing & Resumption)                      |
|  - Turn Watchdog Monitor (Stall Detection & Process Tree Liveness)                |
|  - Deterministic Context Compactor (Token Budget & Prompt Cache Stability)        |
|  - Autonomous Routine Scheduler (Cron / Interval Proactive Execution)             |
|  - Specialist Coordinator & Team Rooms (Multi-Agent Dispatch & Mentions)          |
+-----------------------------------------------------------------------------------+
         │                                                    │
         ▼                                                    ▼
+----------------------------------+       +----------------------------------------+
|      SPARK Memory Subsystem      |       |       SPARK Security & Governance      |
|  - SQLite Memory Journal (ACID)  |       |  - Windows Safety Floor (PowerShell AST)
|  - Topic Notes (MEMORY.md)       |       |  - One-Time HMAC Approval Guard        |
|  - Zero-Token Heuristic Proposer |       |  - DPAPI Secret Vault & Ed25519 Keys   |
+----------------------------------+       +----------------------------------------+
                                   │
                                   ▼
+-----------------------------------------------------------------------------------+
|                        Windows Node Capabilities (INodeCapability)                |
|  - Local Isolated Browser (Edge/WebView2 CDP Pipe, Sandbox Profile, SSRF Guard)   |
|  - Screen & Camera (Windows Graphics Capture, MediaCapture)                       |
|  - Speech & Vision (Whisper.net STT, Kokoro/Piper Neural TTS, Gemini Live)        |
|  - System & Execution (MXC Sandboxing, PowerShell AST Execution)                  |
|  - MCP Tool Bridge (Auto-discovery of INodeCapability on HTTP:18793)             |
+-----------------------------------------------------------------------------------+
```

---

## 3. Subsystem Specifications

### 3.1 Security & Governance (`OpenClaw.Shared.Security`)
- `WindowsSafetyFloor`: Analyzes PowerShell syntax trees via `System.Management.Automation.Language.Parser`. Resolves aliases (`rm`, `del`, `rd`, `ri`), base64-decoded `-EncodedCommand` arguments, and enforces an unbypassable never-list against root deletions, disk formatting, and system boot modifications.
- `HmacApprovalGuard`: Validates approval tickets with cryptographically secure single-use nonces, 60-second time-to-live (TTL), constant-time string comparisons, and in-memory replay prevention.

### 3.2 Memory Subsystem (`OpenClaw.Shared.Memory`)
- `SqliteMemoryJournal`: Maintains an append-only, transactional audit database (`journal.db`) in `%LOCALAPPDATA%\OpenClaw\Memory\` using SQLite WAL mode. Records before/after text hashes, unified diffs, and permits atomic document rollback.
- `MemoryHeuristicProposer`: Scans conversations that have been idle for 60+ seconds, extracting user facts and preferences via zero-token regex heuristics.
- `TopicMemoryStore`: Reads and writes topic-specific markdown notes (`MEMORY.md` and `memory/<topic>.md`) with conflict resolution.

### 3.3 Agent Runtime & Watchdog (`OpenClaw.Shared.Agent` & `OpenClaw.Shared.Watchdog`)
- `AgentTaskManager`: Persists task states (`Queued`, `Running`, `WaitingApproval`, `Completed`, `Failed`) to JSON checkpoint files on disk, ensuring tasks survive unexpected process restarts.
- `TurnWatchdogMonitor`: Tracks active tool executions. Queries Win32 process execution times via `GetProcessTimes` and terminates stalled or dead engine processes while redacting sensitive tokens from notices.

### 3.4 Context Management (`OpenClaw.Chat`)
- `DeterministicContextCompactor`: Folds older exchanges into an excerpted summary header once token counts exceed the configured budget share, preserving prompt caching while keeping recent exchanges verbatim.

### 3.5 Isolated Browser Automation (`OpenClaw.Shared.Capabilities` & `OpenClaw.Shared.Browser`)
- `IsolatedBrowserCapability`: Implements `INodeCapability` to drive Microsoft Edge / WebView2 over `--remote-debugging-pipe`. Enforces a dedicated user profile directory in `%LOCALAPPDATA%\OpenClaw\BrowserProfiles\AgentProfile\` and filters outbound navigation through `BrowserSsrfGuard`.

### 3.6 Proactive Routines & Team Rooms (`OpenClaw.Shared.Routines` & `OpenClaw.Shared.Teams`)
- `AutonomousRoutineScheduler`: Persistent cron and interval timer engine executing routines with unattended run limits.
- `TeamRoomService`: Facilitates multi-agent collaboration with mention parsing, turn origin fencing, and chain-depth limits.
