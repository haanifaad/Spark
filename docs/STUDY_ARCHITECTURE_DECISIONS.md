# SPARK Study Division — Architecture Decision Records (ADRs)

## ADR-001: Quarantined Agent Runtime
- **Status**: Permanently Quarantined (SUPERSEDED by Native Architecture).
- **Context**: `C:\Intel\Spark\agent-runtime` failed independent audits and contained unstable mock dependencies.
- **Decision**: Zero imports, zero references, zero code copying. All functionality is implemented natively in `OpenClaw.Shared`.

## ADR-002: Atomic JSON Storage over Complex Vector Databases for Phase 4
- **Status**: Accepted.
- **Context**: Adding an external vector database (Milvus, Qdrant, Chroma) introduces heavy external C++ / network dependencies that break desktop local offline capability.
- **Decision**: Use `StudyDataStore.cs` with atomic file persistence, thread-safe user concurrency gates, and an in-memory keyword/TF-IDF and semantic chunk index with source references.

## ADR-003: Unified Execution Pipeline
- **Status**: Accepted.
- **Context**: In traditional systems, slash commands and natural language chat run on disconnected code paths.
- **Decision**: Both slash commands and natural language pass through the single `StudyExecutionPipeline` (`User Input -> Intent/Command -> Skill -> Tool -> Permission -> Execution -> Synthesis -> User`).

## ADR-004: Fail-Closed Security Model & Human Approval
- **Status**: Accepted.
- **Context**: Autonomous tools executing arbitrary code or file modifications pose serious local system risks.
- **Decision**: 6 permission tiers (`ReadOnly`, `FileAccess`, `ExternalApiAccess`, `FileModification`, `CodeExecution`, `DestructiveOperation`). Tiers 4, 5, 6 require explicit human approval via modal confirmation dialog.

## ADR-005: Knowledge Grounding & Source Attribution
- **Status**: Accepted.
- **Context**: AI models can hallucinate citations, facts, and page numbers.
- **Decision**: Ingestion preserves exact document chunk provenance (`DocumentId`, `PageNumber`, `SectionTitle`). Retrieved chunks are explicitly cited in responses with bracketed source badges (e.g. `[Doc: Quantum-Computing.pdf, Page: 4]`).
