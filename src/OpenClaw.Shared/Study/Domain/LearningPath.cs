using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// A structured, progressive sequence of courses, modules, and lessons guiding a learner
/// from prerequisite fundamentals to domain mastery across universal education levels.
/// </summary>
public sealed class LearningPath
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EducationLevel Level { get; set; } = EducationLevel.University;
    public string TargetAudience { get; set; } = string.Empty;
    public List<string> OrderedCourseIds { get; set; } = [];
    public List<string> OrderedLessonIds { get; set; } = [];
    public int EstimatedTotalHours { get; set; } = 40;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public override string ToString() => $"{Title} ({Level}) - {EstimatedTotalHours}h";
}
