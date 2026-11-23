# SPARK MASTER INTEGRATION MATRIX

## 1. Overview & Architectural Boundaries

The SPARK system coordinates five core application divisions across a shared presentation layer (WinUI 3 on .NET 10) and an autonomous background runtime layer:
1. **Chat Division**: Conversational interaction, multi-turn reasoning, bilingual English/Malayalam handling, model selection, and tool execution.
2. **Work Division**: Autonomous planning, task decomposition, background execution, worker scheduling, spending controls, and progress tracking.
3. **Code Division**: Developer workspace, repository analysis, cyber security auditing, reverse engineering, CLI tool execution, and Git automation.
4. **Study Division**: Course and topic curriculum synthesis, multimodal document comprehension, pedagogical instruction, and bilingual tutoring.
5. **Creative Division**: Studio media authoring, app/skill discovery, local generative pipeline dispatch, and asset workflows.

---

## 2. Cross-Division Communication Matrix

| From Division | To Division | Expected Integration Behavior | Actual Mechanism Discovered | Status | Evidence / Notes |
|---|---|---|---|---|---|
| **Chat** | **Work** | Delegate actionable task to autonomous task planner/state manager | `OpenClawChatDataProvider` -> Gateway RPC -> `SparkTaskStateManager` / `IAgentOrchestrator` | **PASS** | Chat passes natural language directives that decompose via `SparkTaskDecomposer` into discrete lifecycle steps. |
| **Chat** | **Code** | Hand off code generation, repository edits, or security audit | Direct navigation dispatch via `DivisionManager.SwitchDivisionAsync(SparkDivision.Code)` and workspace context sharing | **PASS** | Shared workspace root (`C:\Intel\Spark\Spark-main`) is accessible; `CodingDivisionService` receives shared context. |
| **Chat** | **Study** | Transfer query for in-depth educational explanation or curriculum reference | Context bridge via `TopicMemoryStore` & `StructuredMemoryJournal` | **PASS** | Memory records tagged with domain categories are queryable cross-divisionally. |
| **Chat** | **Creative** | Trigger generative image/video synthesis and receive artifacts | Tool invocation via `SparkToolRegistry` and `CreativeAppRegistry` | **PASS** | Creative tools registered in `SparkToolRegistry` can be scheduled and rendered in timeline. |
| **Work** | **Code** | Execute automated coding routines and CLI test/build passes | `SparkAgentOrchestrator` -> `ICodingDivisionService` & `SparkCliHubCatalog` | **PASS** | Work background executor invokes CLI tools and development tasks via `CliEngine`. |
| **Work** | **Study** | Execute structured study sessions, quiz generation, and progress commits | `SparkAutonomousScheduler` -> `IStudyDivisionService` / `StudyTutorService` | **PASS** | Scheduled study reminders and autonomous curriculum progression supported via `AutonomousRoutineScheduler`. |
| **Work** | **Creative** | Automate scheduled asset rendering, thumbnail generation, or conversion | Background worker cycle -> `CreativeProjectManager` / `CreativeMcpGateway` | **PASS** | Background worker executes queued creative tasks during scheduled non-quiet windows. |
| **Code** | **Work** | Report compilation, testing progress, and audit findings to workboard | `ICodingDivisionService.AuditEngine` events -> `TaskStateManager` | **PASS** | Security findings and file change diffs update execution logs and state records. |
| **Study** | **Chat** | Share pedagogical insights, study citations, and review summaries back to user | Shared session notes written to `StructuredMemoryJournal` | **PASS** | Notes and session citations are persisted to the shared SQLite journal and retrievable in Chat. |
| **Creative** | **Chat** | Return generated image/audio/video files for direct inline presentation | `ChatAssistantMediaRenderer` & `GatewayMediaMessageProjection` | **PASS** | Generated artifact URLs/local file paths render directly as attachments in the chat timeline. |

---

## 3. Shared Infrastructure & Data Synchronization

### A. Memory Synchronization
- **Central Store**: `SparkMemoryStore` (backed by SQLite and in-memory caches) with `StructuredMemoryJournal` and `TopicMemoryStore`.
- **Cross-Division Isolation**: Scopes (`PersonalFact`, `Preference`, `ProjectContext`, `StudyCitation`, `CodingPattern`) enforce structured classification while permitting global queryability by authorized services.
- **Cache Invalidation**: Write-through updates prevent stale reads across concurrent scopes.

### B. Security & Sentinel Gate
- **Sentinel Enforcement**: All destructive actions (filesystem write outside authorized directory, arbitrary shell commands, payment commitments) must pass through `ISentinelReviewer` and `ISparkApprovalManager`.
- **Fail-Closed Behavior**: If an approval is rejected or unhandled, execution halts immediately without side-effects.

### C. AI Model Router
- **Central Router**: `SparkModelRouter` handles multi-provider discovery (Ollama, local GGUF, cloud providers) and fallback failover.
- **Context Handling**: Enforces model-specific context window budgets, preventing catastrophic token truncation.
