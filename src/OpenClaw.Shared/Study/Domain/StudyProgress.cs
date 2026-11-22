using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Learner mastery classification for a curriculum, subject, or lesson.
/// </summary>
public enum MasteryTier
{
    Novice,
    Competent,
    Proficient,
    Master
}

/// <summary>
/// Tracks user learning progress, lesson completions, and mastery metrics.
/// </summary>
public sealed class StudyProgress
{
    public string UserId { get; set; } = "default";
    public string CourseId { get; set; } = string.Empty;
    public string? ActiveLessonId { get; set; }
    public double CompletionPercentage { get; set; }
    public DateTime? LastActivityUtc { get; set; } = DateTime.UtcNow;
    public HashSet<string> CompletedLessonIds { get; set; } = [];
    public MasteryTier MasteryTier { get; set; } = MasteryTier.Novice;
    public double OverallScore { get; set; }
    public DateTime LastStudiedUtc { get; set; } = DateTime.UtcNow;
    public int StreakDays { get; set; } = 1;

    public override string ToString() => $"Course {CourseId} - {MasteryTier} ({CompletedLessonIds.Count} lessons completed)";
}
