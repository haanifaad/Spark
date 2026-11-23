# SPARK Capability Research

## 1. Executive Overview

This document presents a technical investigation into the four primary commercial and open-source personal AI agent platforms:
1. OpenAI Dots
2. xAI Grok Bot
3. Meta Muse
4. Anthropic Claude Duet

Along with their open-source equivalents and inspirations:
- Rakazo (self-hosted Grok Bot / Dots alternative)
- OpenMausBot (Grok Bot open-source alternative with turn watchdog)
- OpenFuse (formerly OpenMuse; e2b microVM computer agent)
- nanoMuse (lightweight multi-platform agent with HMAC confirmation tickets)
- Suparagent / Open Duet (lineage: open-dots -> Open Duet -> Suparagent)
- Agent Zero, OpenHands, and Hermes Agent

The objective is to synthesize their verified engineering patterns, protocols, and workflows to transform SPARK into a unified personal AI operating environment on Windows.

---

## 2. Primary Targets: Capability Breakdown

### A. OpenAI Dots
- Always-on personal agent architecture: Maintains a dedicated cloud or desktop runtime that remains active across user sessions.
- Persistent background work: Executes multi-step routines asynchronously, decoupling user presence from task completion.
- Dedicated computer and browser: Operates in a sandboxed execution container with browser DOM automation and file manipulation.
- Multiple concurrent projects: Isolates workspace state, notes, and task queues per project.
- Long-term context and preference learning: Extracts user habits and recurring instructions into an enduring profile.
- Voice communication: Real-time, low-latency audio interaction with interruption awareness.
- Progress reporting: Continuous activity streams reporting tool actions, planning iterations, and completed milestones.

### B. xAI Grok Bot
- Persistent bot identities: Named agent personas with distinct system instructions, assigned tools, and visual avatars.
- Agent-to-agent handoffs: Specialized bots delegating subtasks to peer bots with structured handoff blocks.
- Teams of specialist bots: Shared chat spaces (team rooms) where bots communicate, monitor mentions, and collaborate.
- Voice calls and voice notes: Bidirectional voice streams and asynchronous voice memo transcription and synthesis.
- Scheduled routines: Cron-driven and interval-driven autonomous triggers with unattended iteration limits.
- Turn watchdog: Active monitoring of tool execution, dead-process detection, and stalled command alerting.

### C. Meta Muse
- Isolated computer sandbox: Disposable microVM execution (such as e2b desktop) providing full Linux GUI and VNC streaming.
- Goal-based planning: Recursive task decomposition breaking high-level objectives into verifiable milestones.
- Proactive suggestions: Suggests next actions based on detected project state and upcoming schedule items.
- Approval-before-action architecture: Enforces human-in-the-loop review for file writes, network egress, and payment gates.
- Activity history: Immutable changelogs of all agent decisions, tool arguments, and output artifacts.

### D. Anthropic Claude Duet
- Proactive personal assistance: Morning briefings, inbox triage, and automated calendar summaries.
- Persistent conversations: Session resumption without state loss, surviving backend process restarts.
- Memory and skills continuity: Topic-based notes and procedural skills maintained across sessions.
- Cloud and local connector ecosystem: Standardized integration via Model Context Protocol (MCP) and third-party APIs.
- Human approval for sensitive operations: Unbypassable security floors preventing destructive operations.

---

## 3. Verified Architectural Patterns from Reference Systems

### 1. Rakazo (Packages/Memory, Packages/Core, Packages/Adapters)
- Verified Pattern: Space-scoped and Bot-scoped Markdown memory stores with optimistic revision tokens.
- SSRF Filtering: DNS pre-resolution checking against RFC-1918, RFC-3927 (link-local), and loopback ranges before web requests.
- Lazy Tool Catalog: Dynamically advertising tool definitions to the model only when relevant skill scopes are loaded.

### 2. OpenMausBot (Server/turn-watchdog.ts, Server/vps-computer.ts)
- Verified Pattern: Active Turn Watchdog tracking tool start/end timestamps and child process PID liveness.
- Unattended Turn Ceilings: Hard bounds on consecutive tool failures and total autonomous steps during scheduled routines.
- Secret Redaction in Notices: Scrubbing sensitive tokens and API keys from error messages and user notifications.

### 3. OpenFuse (Apps/Server/Src/computer-e2b-desktop.ts, Apps/Server/Src/engine/tanstack-agent.ts)
- Verified Pattern: Unified provider schema (`provider/model`) supporting OpenAI, Anthropic, and Gemini with automated exponential retry loops.
- Disposable Desktop MicroVMs: Cloud sandbox integration with VNC video streaming and automated 15-minute idle pausing.

### 4. nanoMuse (Nanomuse/Bridge/mcp_server.py, Nanomuse/Tools/computer.py)
- Verified Pattern: HMAC Approval Tickets binding user confirmation to canonical tool arguments.
- Proactive Trigger Store: Monitoring incoming emails and calendar feeds to trigger background agent workflows.

### 5. Suparagent / Open Duet (Backend/App/Services/safety_floor.py, memory_journal.py, context_compaction.py)
- Verified Pattern: Unbypassable Never-List Safety Floor preventing catastrophic command execution regardless of user allow rules.
- Undoable Memory Journal: Audit trail recording before/after hashes, unified diffs, and 1-click rollback outside agent workspaces.
- Heuristic Memory Proposer: Zero-token regex extraction of user preferences from idle conversations.
- Deterministic Context Compactor: Folding older transcript exchanges into structured excerpt headers to maintain prompt cache stability.
- Multi-Agent Rooms: Lock-safe dispatching of messages across specialist agents with chain-depth limits.

---

## 4. Synthesis for SPARK

By incorporating these verified engineering patterns into SPARK's native .NET 10 / WinUI 3 architecture, SPARK achieves:
1. Native Windows safety through PowerShell AST analysis and HMAC approval guards.
2. ACID-compliant SQLite memory journaling and zero-token preference harvesting.
3. Turn watchdog protection backed by Win32 Job Objects.
4. Deterministic transcript compaction eliminating context window overflows.
5. In-process Microsoft Edge / WebView2 browser automation over secure CDP pipes.
6. Specialist agent team rooms with structured subtask delegation.
