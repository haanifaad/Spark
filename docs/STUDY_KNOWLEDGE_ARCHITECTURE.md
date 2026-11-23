# SPARK Study Division — Knowledge Architecture
## Personal Knowledge Repository, Document Lifecycle & Chunk Schema

### 1. Architectural Overview

The SPARK Study Division Knowledge Repository provides local-first, multi-tenant, privacy-preserving document storage and semantic chunking for learners. The architecture is built natively inside `Spark-main` (`OpenClaw.Shared.Study.Knowledge`), leveraging the fail-closed persistence model of `StudyDataStore`.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        User / WinUI 3 Frontend                         │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│               IStudyDivisionService / IStudyApiService                 │
└───────┬───────────────────────────┬────────────────────────────┬───────┘
        │                           │                            │
        ▼                           ▼                            ▼
┌───────────────────┐     ┌───────────────────┐       ┌──────────────────┐
│ Knowledge Service │     │ Ingestion Engine  │       │ Grounded Search  │
│ (Collections &    │     │ (Format Parsers & │       │ (Multi-factor    │
│  Provenance)      │     │  Page Chunking)   │       │  Ranking & RAG)  │
└─────────┬─────────┘     └─────────┬─────────┘       └─────────┬────────┘
          │                         │                           │
          └─────────────────────────┼───────────────────────────┘
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│            IStudyDataStore (Atomic JSON Collections v1.3.0)            │
│  - knowledge_collections.json     - documents.json                     │
│  - document_chunks.json           - processing_jobs.json               │
│  - research_projects.json         - research_questions.json            │
│  - research_notes.json            - research_evidence.json             │
│  - research_citations.json                                             │
└────────────────────────────────────────────────────────────────────────┘
```

---

### 2. Domain Models & Schema

#### 2.1 KnowledgeCollection
Logical grouping of study materials, textbooks, lecture slides, and notes for a specific topic or course:
- `Id`: Unique collection identifier (`col-xxxx`).
- `UserId`: Strict user isolation boundary.
- `Title`: Descriptive collection name (e.g., "Advanced Quantum Mechanics").
- `Description`: Subject overview and context.
- `SubjectId` / `CourseId`: Optional linkage to SPARK Study courses.
- `Tags`: Categorization tags.
- `DocumentIds`: List of associated document IDs.
- `CreatedAtUtc` / `UpdatedAtUtc`: Provenance timestamps.

#### 2.2 StudyDocument
Registered study artifact:
- `Id`: Unique document identifier (`doc-xxxx`).
- `UserId`: Multi-tenant boundary.
- `FileName`: Original filename (e.g., `quantum_notes.md`).
- `FilePath`: Absolute path on disk or `memory://{name}` for ephemeral streams.
- `FileSizeBytes`: Source file size.
- `Format`: Normalized lowercase extension (`.txt`, `.md`, `.json`, `.csv`, `.pdf`, `.docx`, etc.).
- `Title`: Normalized display title without file extension and delimiters.
- `Author`: Document creator or publisher.
- `CollectionId`: Parent collection identifier.
- `PageCount`: Detected or computed page count.
- `CharacterCount`: Total raw extracted characters.
- `Status`: `Pending` | `Processing` | `Indexed` | `Failed` | `Reprocessing`.
- `Sha256Checksum`: Cryptographic digest for duplicate detection and tamper validation.
- `Metadata`: Key-value properties (e.g., `RawContent`, OCR confidence, extraction engine).

#### 2.3 DocumentChunk
Semantically coherent segment retaining explicit source provenance:
- `Id`: Unique chunk identifier (`chk-xxxx`).
- `DocumentId`: Foreign key to parent `StudyDocument`.
- `UserId`: Multi-tenant owner.
- `ChunkIndex`: Zero-based sequential order within document.
- `PageNumber`: 1-based page location where text originated.
- `SectionTitle`: Detected heading or section banner (e.g., "Wave-Particle Duality").
- `Content`: Clean extracted text.
- `TokenCount`: Estimated token volume (character length / 4).
- `Keywords`: Key academic terms extracted for fast inverted token retrieval.

---

### 3. Document Processing Lifecycle

1. **Registration & Validation**:
   - Client submits file path or raw stream.
   - Extension validated against supported whitelist (`.txt`, `.md`, `.markdown`, `.json`, `.csv`, `.html`, `.htm`, `.pdf`, `.docx`, `.epub`).
   - SHA256 checksum calculated over content bytes.
   - If identical checksum exists for user in collection, existing document record is returned (zero duplicate chunk pollution).

2. **Extraction & Pagination**:
   - Text extracted preserving natural page dividers (`---page---`, form-feeds `\f`, or 45-line blocks).
   - HTML tags stripped; markdown headers parsed for section attribution.

3. **Chunking & Indexing**:
   - Chunks constructed at paragraph and heading boundaries.
   - Words >= 4 characters extracted as indexed search keywords.
   - Chunks atomically persisted to `document_chunks.json`.
   - Document status transitioned to `Indexed`.
