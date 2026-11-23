using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Base class for all Study Division domain and system events.
/// </summary>
public abstract record StudyEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string UserId { get; init; } = "default";
    public abstract string EventType { get; }
}

public sealed record StudySessionStartedEvent(
    string SessionId,
    string CourseId,
    string? LessonId) : StudyEvent
{
    public override string EventType => "study.session.started";
}

public sealed record StudyMessageReceivedEvent(
    string SessionId,
    string Content,
    string Sender) : StudyEvent
{
    public override string EventType => "study.message.received";
}

public sealed record StudyLessonCompletedEvent(
    string CourseId,
    string LessonId,
    double Score) : StudyEvent
{
    public override string EventType => "study.lesson.completed";
}

public sealed record StudyProgressUpdatedEvent(
    string CourseId,
    string MasteryTier,
    int CompletedCount) : StudyEvent
{
    public override string EventType => "study.progress.updated";
}

public sealed record StudyApprovalRequiredEvent(
    string OperationId,
    string ActionName,
    string Description) : StudyEvent
{
    public override string EventType => "study.approval_required";
}

/// <summary>
/// Event bus interface for the Study Division.
/// </summary>
public interface IStudyEventBus
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : StudyEvent;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : StudyEvent;
}
