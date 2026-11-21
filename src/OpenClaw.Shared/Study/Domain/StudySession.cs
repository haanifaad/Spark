using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Execution state of an interactive learning session.
/// </summary>
public enum StudySessionStatus
{
    Active,
    Paused,
    Completed
}

/// <summary>
/// Sender of a message in a study session.
/// </summary>
public enum StudyMessageSender
{
    User,
    Tutor,
    System
}

/// <summary>
/// Message exchanged during an educational learning turn.
/// </summary>
public sealed record StudySessionMessage(
    string Id,
    StudyMessageSender Sender,
    string Content,
    DateTime TimestampUtc,
    string? Intent = null);

/// <summary>
/// Represents an active or archived educational learning session.
/// </summary>
public sealed class StudySession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "default";
    public string CourseId { get; set; } = string.Empty;
    public string? LessonId { get; set; }
    public string Title { get; set; } = "Learning Session";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public StudySessionStatus Status { get; set; } = StudySessionStatus.Active;
    public List<StudySessionMessage> Messages { get; set; } = [];
    public string Notes { get; set; } = string.Empty;

    public override string ToString() => $"{Title} [{Status}]";
}
