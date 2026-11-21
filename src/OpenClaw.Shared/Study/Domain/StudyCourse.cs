using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Educational levels supported across the Universal AI Education roadmap.
/// </summary>
public enum EducationLevel
{
    School,
    College,
    University,
    Professional,
    Technical,
    Research
}

/// <summary>
/// Types of pedagogical content supported by a lesson.
/// </summary>
public enum LessonType
{
    Concept,
    Practice,
    Reading,
    Quiz,
    Lab
}

/// <summary>
/// Represents a primary course within the Study Division.
/// </summary>
public sealed class StudyCourse
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "default";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EducationLevel Level { get; set; } = EducationLevel.University;
    public List<StudySubject> Subjects { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public override string ToString() => $"{Title} ({Level})";
}

/// <summary>
/// Represents a subject or major disciplinary area within a course.
/// </summary>
public sealed class StudySubject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CourseId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<StudyChapter> Chapters { get; set; } = [];

    public override string ToString() => Name;
}

/// <summary>
/// Represents a logical grouping or module of lessons within a subject.
/// </summary>
public sealed class StudyChapter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SubjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<StudyLesson> Lessons { get; set; } = [];

    public override string ToString() => Title;
}

/// <summary>
/// Atomic unit of instruction or learning activity.
/// </summary>
public sealed class StudyLesson
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ChapterId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public LessonType Type { get; set; } = LessonType.Concept;
    public int EstimatedMinutes { get; set; } = 15;
    public int Order { get; set; }

    public override string ToString() => Title;
}
