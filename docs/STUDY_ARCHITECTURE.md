# SPARK STUDY DIVISION — ARCHITECTURE SPECIFICATION
## Phase 1: Foundation & Architecture Baseline

---

## 1. Executive Vision & Scope

The **SPARK Study Division** is an integrated architectural division within the SPARK Native Windows AI Assistant & Agent Hub. While SPARK currently provides conversational AI (`Chat`), enterprise/personal productivity (`Work`), and autonomous software development (`Code`), the **Study Division** establishes a dedicated, first-class learning, research, reading, and knowledge synthesis environment.

Eventually, the Study Division will support:
- Primary, secondary, higher, and vocational education
- University and post-graduate scientific research
- AI-assisted socratic tutoring and adaptive curriculum navigation
- Custom educational Skills, Tools, Commands, and Agents
- Local, cloud, and hybrid inference routing with strict privacy isolation
- Safe, manual-first action execution requiring user approval

**Phase 1** does not build all educational features. Instead, Phase 1 establishes the rock-solid architectural, domain, API, database, security, and UI foundations required by subsequent phases without modifying or destabilizing existing SPARK services.

---

## 2. Existing SPARK Architecture Audit

An inspection of the SPARK codebase reveals several key architectural characteristics and reusable assets:

| Subsystem | Existing Implementation | Reusability in Study Division |
| :--- | :--- | :--- |
| **Architectural Divisions** | `OpenClaw.Shared.Divisions.SparkDivision` defines `Chat`, `Work`, `Code`, `Study`, `Creative`. | `SparkDivision.Study` is already recognized in `DivisionCatalog` with route key `"study"`. Activated in Phase 1 (`IsActive = true`). |
| **Model Routing** | `ISparkModelRouter` & `SparkModelRouter` (`OpenClaw.Shared.Inference.Routing`). Supports capability, cost, latency, locality, and privacy filtering. | Fully reused. Study AI requests route through `ISparkModelRouter` without duplicating model selection logic. |
| **Memory & Storage** | `ISparkMemoryStore` & `SparkMemoryStore` (`OpenClaw.Shared.Memory`). Multi-tier partitioned memory with BM25 indexing and atomic JSON persistence. | Reused for episodic learning memory, concept retention, and study context lookup. |
| **Agent Orchestration** | `IAgentOrchestrator` & `SparkAgentOrchestrator` (`OpenClaw.Shared.Orchestration`). | Study Division introduces `IStudyOrchestrator` conforming to SPARK task lifecycle patterns. |
| **Security & Approvals** | `ISparkApprovalManager` (`OpenClaw.Shared.Security.Approvals`) and `ISparkAuditLogger`. | All sensitive operations (file export, network downloads, code evaluation) require manual user approval. |
| **UI Presentation** | WinUI 3 desktop client (`OpenClaw.Tray.WinUI`), Fluent Design, Mica backdrop, `HubPageRegistry`. | Native `StudyPage.xaml` integrated directly into navigation and hub page resolution. |

---

## 3. Logical Architecture & Boundaries

The Study Division is organized along six strict logical boundaries:

```
+--------------------------------------------------------------------------+
|                             Study.Presentation                           |
|      (WinUI 3 StudyPage, StudyPageViewModel, Navigation, Fluent Shell)   |
+------------------------------------+-------------------------------------+
                                     |
+------------------------------------v-------------------------------------+
|                             Study.Application                            |
|    (IStudyDivisionService, StudyApiService, Request/Response DTOs)       |
+---------+--------------------------+---------------------------+---------+
          |                          |                           |
+---------v----------+      +--------v---------+       +---------v---------+
|    Study.Domain    |      |Study.Orchestrator|       |Study.Infrastruct. |
| Courses, Subjects, |      |Intent Routing,   |       |Atomic DataStore,  |
| Chapters, Lessons, |      |Context Builder,  |       |Event Bus, Migrat.,|
| Sessions, Progress |      |Model Selection   |       |Security Manager   |
+---------+----------+      +--------+---------+       +---------+---------+
          |                          |                           |
+---------v--------------------------v---------------------------v---------+
|                              Study.Contracts                             |
|      (IStudyDataStore, IStudyApiService, IStudyOrchestrator,             |
|       IStudyPermissionManager, IStudyEventBus, Future Tool/Skill Specs)  |
+------------------------------------+-------------------------------------+
                                     |
+------------------------------------v-------------------------------------+
|                          SPARK Shared Services                           |
|       (ISparkModelRouter, ISparkMemoryStore, ISparkApprovalManager)      |
+--------------------------------------------------------------------------+
```

### Module Responsibilities:

1. **Study.Contracts**: Pure interfaces and shared primitives. Ensures loose coupling and zero circular dependencies.
2. **Study.Domain**: Rich domain entities representing education levels, courses, subjects, chapters, lessons, learning sessions, and mastery progress.
3. **Study.Application**: API surface (`/api/v1/study/*`), service facades (`StudyDivisionService`), validation, and correlation.
4. **Study.Infrastructure**: Thread-safe ACID-compliant JSON datastore with atomic file writing, schema migration runner, in-memory event bus, and permission verification.
5. **Study.Orchestration**: Intent classification (explanation, practice, socratic inquiry, exam review), context aggregation, and model selection.
6. **Study.Presentation**: WinUI 3 user interface providing responsive workspace layout, course catalog, active study session, and progress insights.

---

## 4. Module Ownership Matrix

| Domain Module | Primary Agent Owner | In-Code Namespace | Physical Location |
| :--- | :--- | :--- | :--- |
| **Architecture & Contracts** | Architect Agent | `OpenClaw.Shared.Study.Contracts` | `src/OpenClaw.Shared/Study/Contracts/` |
| **Domain Entities** | Architect & Engineering | `OpenClaw.Shared.Study.Domain` | `src/OpenClaw.Shared/Study/Domain/` |
| **AI Orchestration** | AI Intelligence Agent | `OpenClaw.Shared.Study.Orchestration`| `src/OpenClaw.Shared/Study/Orchestration/`|
| **DataStore & API** | Engineering Agent | `OpenClaw.Shared.Study.Infrastructure`| `src/OpenClaw.Shared/Study/Infrastructure/`|
| **Security & Permissions**| Engineering Agent | `OpenClaw.Shared.Study.Security` | `src/OpenClaw.Shared/Study/Security/` |
| **WinUI Presentation** | Experience & QA Agent | `OpenClawTray.Pages`, `ViewModels` | `src/OpenClaw.Tray.WinUI/Pages/`, `ViewModels/`|
| **Verification & Tests** | Experience & QA Agent | `OpenClaw.Shared.Tests.Study` | `tests/OpenClaw.Shared.Tests/Study/` |

---

## 5. Architectural Decision Records (ADRs)

### ADR-01: In-Process Unified Library vs Standalone Microservice
- **Status**: Accepted
- **Context**: SPARK is a native Windows desktop client with low latency requirements.
- **Decision**: Implement the Study Division as a native modular subsystem within `OpenClaw.Shared` and `OpenClaw.Tray.WinUI`, avoiding process overhead and network serialization.

### ADR-02: Storage Engine Selection
- **Status**: Accepted
- **Context**: A full SQL server (e.g., PostgreSQL) or distributed cache (Redis) adds unnecessary system prerequisites and install hurdles for desktop users.
- **Decision**: Adopt SPARK's proven ACID atomic JSON file datastore pattern (`StudyDataStore`) with versioned schema migrations, supporting multi-user isolation and zero external dependencies.

### ADR-03: Model Router Integration
- **Status**: Accepted
- **Context**: SPARK already possesses `ISparkModelRouter` with local Ollama, cloud, and privacy policies.
- **Decision**: Study Division AI orchestrator delegates all model selection and execution directly to `ISparkModelRouter`.

### ADR-04: Security Policy — Manual Execution by Default
- **Status**: Accepted
- **Context**: Educational tasks might involve evaluating student code or downloading external learning assets.
- **Decision**: Default to manual confirmation for any file modification, external API call, or code execution. No background autonomous execution without explicit user consent.
