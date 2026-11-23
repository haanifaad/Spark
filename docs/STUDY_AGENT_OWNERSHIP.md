# SPARK Study Division — Agent Ownership Map

## 1. Ten Specialist Subagent Architecture
To achieve maximum safe parallelism during the development of SPARK Study Division, responsibilities and file ownership are strictly partitioned across 10 specialized subagents:

| Agent # | Agent Name / Role | File & Module Ownership | Strict Boundaries |
|---|---|---|---|
| **Agent 1** | **Chief Architect & Orchestrator** | Architecture contracts, `docs/STUDY_*.md`, `IStudyKnowledgeService.cs`, `IStudyResearchService.cs` | Coordinates all agents; resolves interface conflicts; enforces quarantine of `agent-runtime`. |
| **Agent 2** | **Learning Domain Intelligence** | `StudyKnowledgeModels.cs`, `StudyResearchModels.cs`, Course/Subject/Chapter relationships | Preserves existing Bloom's taxonomy & pedagogical modes; no duplicate course models. |
| **Agent 3** | **Knowledge Ingestion & Processing** | `IStudyDocumentIngestionService.cs`, `StudyDocumentIngestionService.cs`, `STUDY_DOCUMENT_INGESTION.md` | Non-mock file validation, text extraction (PDF, DOCX, TXT, MD, JSON), chunking, deduplication. |
| **Agent 4** | **Retrieval, Search & Research** | `IStudySearchService.cs`, `StudySearchService.cs`, `StudyResearchService.cs`, `STUDY_RETRIEVAL_ARCHITECTURE.md`, `STUDY_RESEARCH_WORKSPACE.md` | Grounded search, TF-IDF ranking, evidence extraction, citation formatting; no fabricated data. |
| **Agent 5** | **Assessment & Evaluation** | `docs/STUDY_PHASE_5_SPECIFICATION.md` | Boundary review and future contract planning ONLY. **Zero Phase 5 implementation during Phase 4.** |
| **Agent 6** | **Frontend & Study Workspace** | `StudyPage.xaml`, `StudyPage.xaml.cs` (Knowledge Dashboard, Research Workspace views) | Reuses WinUI 3 controls; no hardcoded mocks; respects backend permission gates. |
| **Agent 7** | **AI Intelligence & Orchestration** | `StudyGroundedAnswerSynthesizer.cs`, `StudyExecutionPipeline.cs` retrieval integration | Uses existing `ISparkModelRouter`; grounds answers with citations; handles Malayalam & English. |
| **Agent 8** | **Backend & Infrastructure** | `StudyDatabaseMigrations.cs` (v1.3.0), `StudyDataStore.cs` (atomic JSON stores), `StudyApiService.cs` | Atomic JSON persistence; pagination; transaction gates; no new unproven databases. |
| **Agent 9** | **Security & Permissions** | `StudyPermissionManager.cs` knowledge permissions, `StudyKnowledgeSecurityTests.cs` | Path traversal prevention, user data isolation, fail-closed access controls. |
| **Agent 10** | **Quality Engineering & Testing** | `StudyKnowledgeIngestionTests.cs`, `StudySearchAndRetrievalTests.cs`, `StudyResearchWorkspaceTests.cs`, `StudyGroundedAiTests.cs` | Independent verification; 100% build & test execution; reports limitations honestly. |

---

## 2. Interaction & Conflict Rules
1. **Shared Contract Gate**: Shared interfaces must be approved by Agent 1 before dependent implementation.
2. **Single Primary Owner**: Each file has exactly one primary owner. Secondary modifications must be coordinated.
3. **Continuous Integration**: Integration happens in stages (`Contracts → Domain → Ingestion → Retrieval → AI → Backend → UI → Tests`).
