# SPARK Implementation Roadmap

This roadmap organizes the development of SPARK into structured, sequential implementation tiers with clear validation gates.

---

## Tier 1: P0 Foundation Implementation (Core Security & Memory)

### Step 1.1: Windows Safety Floor
- Location: `src/OpenClaw.Shared/Security/WindowsSafetyFloor.cs`
- Objectives: Implement PowerShell AST token parsing, alias expansion, `-EncodedCommand` decoding, and hard rejection of destructive Windows commands.
- Verification: Unit tests in `tests/OpenClaw.Shared.Tests/Security/WindowsSafetyFloorTests.cs`.

### Step 1.2: One-Time HMAC Approval Guard
- Location: `src/OpenClaw.Shared/Security/HmacApprovalGuard.cs`
- Objectives: Implement ticket generation and validation with single-use nonces, 60-second TTL, constant-time verification, and replay cache.
- Verification: Unit tests verifying nonce replay rejection and expired ticket refusal.

### Step 1.3: SQLite Memory Journal & Heuristic Proposer
- Location: `src/OpenClaw.Shared/Memory/SqliteMemoryJournal.cs` & `MemoryHeuristicProposer.cs`
- Objectives: Create ACID SQLite journal (`journal.db`), unified diff generator, atomic rollback, and zero-token preference extractor.
- Verification: Unit tests for rollback accuracy, WAL concurrency, and preference regex extraction.

---

## Tier 2: P0 Agent Runtime & Watchdog

### Step 2.1: Turn Watchdog Monitor
- Location: `src/OpenClaw.Shared/Watchdog/TurnWatchdogMonitor.cs`
- Objectives: Monitor tool durations, detect silent hangs or dead engine processes, and generate secret-redacted stall notices.
- Verification: Unit tests for stall trip thresholds and PID liveness validation.

### Step 2.2: Deterministic Context Compactor
- Location: `src/OpenClaw.Chat/DeterministicContextCompactor.cs`
- Objectives: In-memory transcript folding preserving recent exchanges verbatim while synthesizing structured excerpts for older history.
- Verification: Unit tests validating fold boundaries and token budget reductions.

---

## Tier 3: P1 Autonomous Execution & Browser

### Step 3.1: Autonomous Routine Scheduler
- Location: `src/OpenClaw.Shared/Routines/AutonomousRoutineScheduler.cs`
- Objectives: Cron/interval background scheduler with unattended execution ceilings and notification throttling.
- Verification: Unit tests for cron timing and runaway iteration prevention.

### Step 3.2: Isolated Native Browser Capability
- Location: `src/OpenClaw.Shared/Capabilities/IsolatedBrowserCapability.cs` & `OpenClaw.Shared/Browser/BrowserSsrfGuard.cs`
- Objectives: Direct Edge/WebView2 CDP automation over pipes with dedicated sandbox profile and loopback SSRF blocks.
- Verification: Unit tests for SSRF blocking on private IP ranges and DOM command execution.

---

## Tier 4: P2 Specialist Teams & Multi-Agent Collaboration

### Step 4.1: Specialist Coordinator & Team Rooms
- Location: `src/OpenClaw.Shared/Teams/SpecialistCoordinator.cs` & `TeamRoomService.cs`
- Objectives: Directory of specialist agent personas, multi-agent room threads, `@agent` mention routing, and subtask delegation with depth limits.
- Verification: Unit tests for mention routing and delegation loop prevention.
