# SPARK Study Division — Phase 5 Architecture Specification
## Assessment, Examination & Evaluation Engine
**Authored by:** Subagent 5 (Assessment & Evaluation Specialist)
**Status:** Architecture Proposal & Specification Only (ZERO Phase 5 Code Implemented in Phase 4)

---

### 1. Architectural Scope & Boundary Analysis

Phase 5 will deliver a comprehensive academic examination, assessment, and grading engine that integrates directly with the Phase 4 Knowledge and Research infrastructure.

#### Key Principles:
1. **Source-Grounded Question Generation**:
   Questions must be derived directly from verified `StudyDocument` chunks and `ResearchEvidence`, eliminating hallucinated test content.
2. **Deterministic & Objective Evaluation**:
   Multiple choice, fill-in-the-blank, and formula questions are evaluated with deterministic rules; open-ended essays utilize rubric-based evaluation.
3. **Adaptive Difficulty**:
   Assessment dynamically adjusts question difficulty based on student mastery tracks.
4. **Offline Capability & Zero Autonomous Execution**:
   All grading algorithms and question banks persist locally via `StudyDataStore`.

---

### 2. Proposed Domain Models for Phase 5

```csharp
namespace OpenClaw.Shared.Study.Domain;

public enum QuestionType
{
    MultipleChoice = 0,
    MultipleSelect = 1,
    TrueFalse = 2,
    ShortAnswer = 3,
    Essay = 4,
    CodeSnippet = 5
}

public enum AssessmentDifficulty
{
    Foundational = 0,
    Intermediate = 1,
    Advanced = 2,
    Mastery = 3
}

public sealed record AssessmentQuestion(
    string Id,
    string CourseId,
    string? SourceDocumentId,
    int? SourcePageNumber,
    string Prompt,
    QuestionType Type,
    AssessmentDifficulty Difficulty,
    IReadOnlyList<string> Options,
    IReadOnlyList<int> CorrectOptionIndices,
    string? ExpectedAnswerText,
    string Explanation,
    int Points = 1
);

public sealed record AssessmentRubric(
    string Id,
    string QuestionId,
    IReadOnlyList<RubricCriterion> Criteria,
    int MaxScore
);

public sealed record RubricCriterion(
    string Name,
    string Description,
    int MaxPoints,
    IReadOnlyList<string> ScoringGuidelines
);

public sealed record ExaminationSession(
    string Id,
    string UserId,
    string CourseId,
    string Title,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int TotalTimeLimitMinutes,
    IReadOnlyList<AssessmentQuestion> Questions,
    Dictionary<string, string> SubmittedAnswers,
    int TotalScore,
    int MaxPossibleScore,
    bool Passed
);
```

---

### 3. Proposed Service Contracts

```csharp
namespace OpenClaw.Shared.Study.Contracts;

public interface IStudyAssessmentService
{
    Task<AssessmentQuestion> GenerateQuestionFromSourceAsync(
        string documentId,
        int pageNumber,
        QuestionType type,
        AssessmentDifficulty difficulty,
        string userId = "default",
        CancellationToken ct = default);

    Task<ExaminationSession> StartExamAsync(
        string courseId,
        string userId = "default",
        int questionCount = 10,
        CancellationToken ct = default);

    Task<ExaminationSession> SubmitAnswerAsync(
        string sessionId,
        string questionId,
        string answer,
        string userId = "default",
        CancellationToken ct = default);

    Task<ExaminationSession> CompleteExamAsync(
        string sessionId,
        string userId = "default",
        CancellationToken ct = default);
}
```

---

### 4. Integration Contract with Phase 4 Knowledge Engine

```
[Phase 4: StudyDocument / DocumentChunk]
                   │
                   ▼ (Extract key concepts & page numbers)
[Phase 5: IStudyAssessmentService.GenerateQuestionFromSourceAsync]
                   │
                   ▼ (Attach SourceDocumentId & PageNumber)
[Phase 5: AssessmentQuestion with Verifiable Provenance]
```

This guarantees that when a learner receives feedback on an incorrect answer, SPARK can link directly back to the exact page and paragraph in their uploaded textbook (`[Source: {doc}, Page {page}]`).
