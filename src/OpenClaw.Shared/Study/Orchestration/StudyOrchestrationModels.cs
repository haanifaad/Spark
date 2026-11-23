using System;
using System.Collections.Generic;
using OpenClaw.Shared.Inference.Routing;

namespace OpenClaw.Shared.Study.Orchestration;

/// <summary>
/// Educational intents recognized by the Study Division AI pipeline.
/// </summary>
public enum StudyEducationalIntent
{
    ConceptualExplanation,
    SocraticTutoring,
    ProblemSolvingPractice,
    SummaryAndSynthesis,
    ExamReview,
    ReferenceLookup
}

public sealed record StudyIntentContext(
    string? CourseTitle = null,
    string? SubjectName = null,
    string? LessonTitle = null);

public sealed record StudyIntentDecision(
    StudyEducationalIntent PrimaryIntent,
    double Confidence,
    string Rationale);

public sealed record StudyContextEnvelope(
    string FormattedContext,
    IReadOnlyList<string> RetrievedMemorySnippets,
    string ActiveSyllabusSnippet);

public sealed record StudyInteractionRequest(
    string Query,
    string UserId = "default",
    string? CourseId = null,
    string? LessonId = null,
    string? SessionId = null,
    bool EnforceLocalOnly = false);

public sealed record StudyInteractionResult(
    string ResponseText,
    StudyEducationalIntent IdentifiedIntent,
    string ModelUsed,
    bool IsFallback,
    long LatencyMs);
