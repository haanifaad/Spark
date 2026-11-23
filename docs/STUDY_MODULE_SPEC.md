# SPARK STUDY DIVISION — MODULE SPECIFICATION
## Phase 1: Subsystem & Component Definition

---

## 1. Module Overview

This specification details the components, boundaries, and dependencies of each Study Division subsystem implemented in Phase 1.

```
                    +------------------------------------+
                    |        Study.Contracts             |
                    +-----------------+------------------+
                                      ^
                                      |
         +----------------------------+----------------------------+
         |                            |                            |
+--------+-------+           +--------+-------+           +--------+-------+
|  Study.Domain  |           | Study.Infrast. |           | Study.Orchestr.|
+--------+-------+           +--------+-------+           +--------+-------+
         ^                            ^                            ^
         |                            |                            |
         +----------------------------+----------------------------+
                                      |
                         +------------+------------+
                         |    Study.Application    |
                         +------------+------------+
                                      ^
                                      |
                         +------------+------------+
                         |   Study.Presentation    |
                         +-------------------------+
```

---

## 2. Subsystem Specifications

### 2.1 Study.Contracts
Namespace: `OpenClaw.Shared.Study.Contracts`

Defines shared interfaces and contract types:
- `IStudyDataStore`: CRUD operations for Courses, Lessons, Sessions, and Progress.
- `IStudyApiService`: API endpoints exposing `/api/v1/study/*`.
- `IStudyOrchestrator`: Entry point for educational AI interactions.
- `IStudyIntentRouter`: Categorizes learner prompts into educational intents.
- `IStudyContextProvider`: Compiles learning context (syllabus, history, preferences).
- `IStudyEventBus`: Event publish/subscribe broker for decoupled notifications.
- `IStudyPermissionManager`: Evaluates and prompts for sensitive operations.
- Future extension placeholders: `IStudySkill`, `IStudyTool`, `IStudyCommand`, `IStudyAgent`, `IStudyExtension`.

### 2.2 Study.Domain
Namespace: `OpenClaw.Shared.Study.Domain`

Defines core business entities and value objects:
- `EducationLevel`: Enum (`School`, `College`, `University`, `Professional`, `Technical`, `Research`).
- `StudyCourse`: Unique course entity with title, description, level, subjects, and creation timestamp.
- `StudySubject`: Subject representation (e.g., "Computer Science", "Organic Chemistry").
- `StudyChapter`: Grouping of related lessons within a subject.
- `StudyLesson`: Atomic learning unit with `LessonType` (`Concept`, `Practice`, `Reading`, `Quiz`, `Lab`).
- `StudySession`: Real-time learning encounter tracking duration, active lesson, and conversation history.
- `StudyProgress`: Tracks user completion, mastery tier (`Novice`, `Competent`, `Proficient`, `Master`), and study streak.

### 2.3 Study.Infrastructure
Namespace: `OpenClaw.Shared.Study.Infrastructure`

Provides concrete implementations of infrastructure contracts:
- `StudyDataStore`:
  - Thread-safe storage with concurrent access coordination (`SemaphoreSlim`).
  - Atomic persistence using `.tmp` buffer files with replace semantics.
  - User isolation via subdirectories under `%APPDATA%\Spark\Study\{userId}\`.
- `StudyDatabaseMigrations`:
  - Migration runner tracking applied schema versions.
  - Initial `1.0.0` migration generating core data catalogs and indexes.
- `StudyEventBus`:
  - In-memory event dispatcher supporting asynchronous event handling.
  - Dispatches `StudySessionStartedEvent`, `StudyLessonCompletedEvent`, etc.
- `StudyPermissionManager`:
  - Implements permission enforcement with manual approval defaults.
  - Gates execution against `ISparkApprovalManager`.

### 2.4 Study.Orchestration
Namespace: `OpenClaw.Shared.Study.Orchestration`

Bridges educational tasks with SPARK's AI intelligence:
- `StudyIntentRouter`:
  - Analyzes learner queries to distinguish between:
    1. `ConceptualExplanation`: Deep concept breakdown.
    2. `SocraticTutoring`: Guiding questions without direct answers.
    3. `ProblemSolvingPractice`: Step-by-step guidance on exercises.
    4. `SummaryAndSynthesis`: Condensed knowledge overview.
    5. `ExamReview`: Rapid quiz preparation.
- `StudyContextProvider`:
  - Assembles prompt envelopes containing user profile, active subject notes, course progress, and episodic memory from `ISparkMemoryStore`.
- `StudyOrchestrator`:
  - Consults `StudyIntentRouter` and `StudyContextProvider`.
  - Formulates routing request to `ISparkModelRouter`.
  - Dispatches inference and logs audit trail.

### 2.5 Study.Application
Namespace: `OpenClaw.Shared.Study.Application`

Orchestrates business workflows and delivers the external API:
- `StudyApiService`:
  - Controller handling `/api/v1/study/*` requests.
  - Implements standard error envelopes, correlation IDs, and pagination.
- `StudyDivisionService`:
  - Singleton facade coordinating UI events, active session state, and background tasks.

### 2.6 Study.Presentation
Namespace: `OpenClawTray.Pages`, `OpenClawTray.ViewModels`

Delivers the desktop user experience:
- `StudyPage.xaml` / `StudyPage.xaml.cs`: WinUI 3 interface with responsive sidebars, course cards, and active lesson workspace.
- `StudyPageViewModel.cs`: Reactive state management.
