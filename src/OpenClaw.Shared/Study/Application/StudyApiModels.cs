using System;
using System.Collections.Generic;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Application;

public sealed record StudyApiError(
    string Code,
    string Message,
    IReadOnlyList<string>? Details = null);

public sealed record StudyApiResponse<T>(
    bool Success,
    T? Data,
    StudyApiError? Error,
    string CorrelationId,
    DateTime TimestampUtc)
{
    public static StudyApiResponse<T> Ok(T data, string? correlationId = null) =>
        new(true, data, null, correlationId ?? Guid.NewGuid().ToString("N"), DateTime.UtcNow);

    public static StudyApiResponse<T> Fail(string code, string message, string? correlationId = null, IReadOnlyList<string>? details = null) =>
        new(false, default, new StudyApiError(code, message, details), correlationId ?? Guid.NewGuid().ToString("N"), DateTime.UtcNow);
}

public sealed record StudyPagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

public sealed record StudyHealthReport(
    string Status,
    string SchemaVersion,
    string DatabaseStatus,
    string ModelRouterStatus,
    int ActiveSessions);

public sealed record CreateCourseRequest(
    string Title,
    string Description,
    EducationLevel Level,
    string? Subject = null);

public sealed record CreateSessionRequest(
    string CourseId,
    string? LessonId = null,
    string Title = "Interactive Learning Session");

public sealed record RecordProgressRequest(
    string CourseId,
    string LessonId,
    double Score,
    MasteryTier Mastery = MasteryTier.Competent);
