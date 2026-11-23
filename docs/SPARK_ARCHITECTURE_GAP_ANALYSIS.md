# SPARK Architecture Gap Analysis

## 1. Scope & Objective

This document analyzes the technical gaps between SPARK's existing implementation and the target architectural capabilities required for a fully autonomous personal AI agent platform.

---

## 2. Capability Gap Evaluation

### Gap 1: Memory Architecture & Auditability
- Current State: SPARK relies on ephemeral chat history and simple JSON configuration files in VoiceMode (`VoiceMode/runtime/memory/memory_manager.py`).
- Target State: Persistent, multi-tier memory system with ACID transaction support, undoable diff journals (`MEMORY.md` and topic notes), and zero-token heuristic preference extraction.
- Severity: High (Blocks long-term personal context).
- Remediation Plan: Implement `SqliteMemoryJournal` and `MemoryHeuristicProposer` in `OpenClaw.Shared/Memory/`.

### Gap 2: Command Execution Safety Boundaries
- Current State: SPARK uses SHA-256 hash matching in `ExecApprovalsStore.cs`. A user could inadvertently grant an "always allow" rule on an ambiguous script prefix.
- Target State: Hardcoded, unbypassable Windows Safety Floor that rejects destructive commands (`Format-Volume`, `diskpart`, `rmdir /s /q C:\`, `bcdedit`, `reg delete HKLM`) regardless of saved rules, combined with single-use HMAC confirmation tickets.
- Severity: Critical (System integrity risk).
- Remediation Plan: Implement `WindowsSafetyFloor` and `HmacApprovalGuard` in `OpenClaw.Shared/Security/`.

### Gap 3: Turn Watchdog & Dead Process Detection
- Current State: SPARK enforces request timeouts via `InvocationCancellationRegistry.cs`, but cannot detect when a child tool process hangs silently or exits prematurely without reporting.
- Target State: Active `TurnWatchdogMonitor` checking execution cadence, CPU time deltas, and child process liveness, with secret-redacted stall notices.
- Severity: High (Risk of frozen agent execution).
- Remediation Plan: Implement `TurnWatchdogMonitor` in `OpenClaw.Shared/Watchdog/`.

### Gap 4: Context Window Management & Compaction
- Current State: SPARK delegates context trimming to the external gateway or truncates older messages.
- Target State: Deterministic context compactor that folds older exchanges into excerpted summaries while retaining recent turns verbatim, stabilizing prompt cache hits and preventing token overflow errors.
- Severity: Medium (Cost optimization and task longevity).
- Remediation Plan: Implement `DeterministicContextCompactor` in `OpenClaw.Chat/`.

### Gap 5: In-Process Browser Automation
- Current State: `BrowserProxyCapability.cs` only forwards calls to an external gateway browser control server.
- Target State: Native browser capability controlling Microsoft Edge / WebView2 via Chrome DevTools Protocol over named pipes, using an isolated sandbox profile and SSRF request filtering.
- Severity: High (Autonomous web task capability).
- Remediation Plan: Implement `IsolatedBrowserCapability` in `OpenClaw.Shared/Capabilities/`.

### Gap 6: Specialist Multi-Agent Collaboration
- Current State: SPARK operates primarily in single-session mode with manual switching or basic handoffs (`sessions_yield`).
- Target State: Team room architecture where specialist agents (Researcher, Coder, Assistant) collaborate with lock-safe message dispatching, mention routing (`@agent`), and chain-depth limits.
- Severity: Medium (Complex project delegation).
- Remediation Plan: Implement `SpecialistCoordinator` and `TeamRoomService` in `OpenClaw.Shared/Teams/`.

### Gap 7: Proactive Background Scheduler
- Current State: SPARK is reactive to user prompts or external gateway heartbeats.
- Target State: Autonomous Windows background routine scheduler executing scheduled research, reminders, and briefings with unattended execution ceilings.
- Severity: Medium (Proactive assistance).
- Remediation Plan: Implement `AutonomousRoutineScheduler` in `OpenClaw.Shared/Routines/`.
