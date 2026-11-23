using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// AI Tutor contract providing pedagogical tutoring, multi-mode instruction,
/// adaptive multi-turn conversations, and bilingual support (English & Malayalam).
/// </summary>
public interface IStudyTutorService
{
    /// <summary>
    /// Generates the next educational tutor turn within an active study session.
    /// Incorporates student level, teaching mode, language, conversation history, and learning objectives.
    /// </summary>
    Task<StudySessionMessage> GenerateTutorTurnAsync(
        StudySession session,
        string userPrompt,
        IReadOnlyList<LearningObjective>? objectives = null,
        CancellationToken ct = default);

    /// <summary>
    /// Builds the structured system prompt defining the AI Tutor's pedagogical personality,
    /// mode-specific guidelines, language conventions, and target audience level.
    /// </summary>
    string BuildSystemPrompt(
        TeachingMode mode,
        LearningLanguage language,
        EducationLevel level,
        string? activeSubject = null,
        string? activeTopic = null,
        IReadOnlyList<LearningObjective>? objectives = null);
}
