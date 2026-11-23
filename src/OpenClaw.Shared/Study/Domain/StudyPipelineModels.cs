using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Origin format of the user request.
/// </summary>
public enum PipelineInputType
{
    SlashCommand,
    NaturalLanguage
}

/// <summary>
/// Unified request envelope for processing user learning queries and commands.
/// </summary>
public sealed class PipelineExecutionRequest
{
    public string UserInput { get; set; } = string.Empty;
    public string UserId { get; set; } = "default";
    public string? SessionId { get; set; }
    public string? CourseId { get; set; }
    public string? LessonId { get; set; }
    public string? ForceSkillId { get; set; }
    public TeachingMode? TeachingMode { get; set; }
    public LearningLanguage? Language { get; set; }
    public EducationLevel? Level { get; set; }

    public PipelineExecutionRequest() { }

    public PipelineExecutionRequest(
        string userInput,
        string userId = "default",
        string? sessionId = null,
        string? courseId = null,
        string? lessonId = null,
        string? forceSkillId = null,
        TeachingMode? teachingMode = null,
        LearningLanguage? language = null,
        EducationLevel? level = null)
    {
        UserInput = userInput;
        UserId = userId;
        SessionId = sessionId;
        CourseId = courseId;
        LessonId = lessonId;
        ForceSkillId = forceSkillId;
        TeachingMode = teachingMode;
        Language = language;
        Level = level;
    }
}

/// <summary>
/// Comprehensive outcome of unified pipeline processing.
/// </summary>
public sealed class PipelineExecutionResult
{
    public string PipelineId { get; set; } = Guid.NewGuid().ToString("N");
    public PipelineInputType InputType { get; set; }
    public ParsedStudyCommand? ResolvedCommand { get; set; }
    public StudySkillDefinition? SelectedSkill { get; set; }
    public List<string> SelectedTools { get; set; } = [];
    public List<StudyToolExecutionResult> ToolResults { get; set; } = [];
    public string FinalResponse { get; set; } = string.Empty;
    public TeachingMode TeachingMode { get; set; } = TeachingMode.Standard;
    public LearningLanguage Language { get; set; } = LearningLanguage.English;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public bool RequiresApproval { get; set; }
    public StudyApprovalInfo? PendingApproval { get; set; }
    public long DurationMs { get; set; }

    public PipelineExecutionResult() { }
}
