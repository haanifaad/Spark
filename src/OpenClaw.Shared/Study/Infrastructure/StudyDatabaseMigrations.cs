using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Infrastructure;

public sealed record SchemaVersionRecord(
    string Version,
    string Description,
    DateTime AppliedAtUtc);

/// <summary>
/// Versioned schema migration runner for the Study Division database foundation.
/// </summary>
public sealed class StudyDatabaseMigrations
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _storageRoot;

    public StudyDatabaseMigrations(string storageRoot)
    {
        _storageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
        Directory.CreateDirectory(_storageRoot);
    }

    private string GetVersionFilePath() => Path.Combine(_storageRoot, "schema_version.json");

    public async Task<IReadOnlyList<SchemaVersionRecord>> GetAppliedMigrationsAsync(CancellationToken ct = default)
    {
        var path = GetVersionFilePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var records = await JsonSerializer.DeserializeAsync<List<SchemaVersionRecord>>(stream, JsonOptions, ct);
            return records ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task ApplyMigrationsAsync(CancellationToken ct = default)
    {
        var applied = (await GetAppliedMigrationsAsync(ct)).ToList();

        // Migration 1.0.0: Initial Baseline Schema
        if (!applied.Any(m => m.Version == "1.0.0"))
        {
            await ApplyMigration_1_0_0(ct);
            applied.Add(new SchemaVersionRecord("1.0.0", "Initial Study Division database baseline with sample curricula.", DateTime.UtcNow));
            await SaveAppliedMigrationsAsync(applied, ct);
        }
    }

    private async Task ApplyMigration_1_0_0(CancellationToken ct)
    {
        var defaultUserDir = Path.Combine(_storageRoot, "default");
        Directory.CreateDirectory(defaultUserDir);

        var coursesPath = Path.Combine(defaultUserDir, "courses.json");
        if (!File.Exists(coursesPath))
        {
            // Seed foundational starter course
            var sampleCourse = new StudyCourse
            {
                Id = "course-foundation-cs",
                UserId = "default",
                Title = "Computer Science Foundations & AI",
                Description = "A complete primer covering algorithms, data structures, and intelligent agents.",
                Level = EducationLevel.University,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Subjects =
                [
                    new StudySubject
                    {
                        Id = "subj-algorithms",
                        CourseId = "course-foundation-cs",
                        Name = "Algorithms and Complexity",
                        Description = "Algorithmic thinking, Big-O notation, and graph traversal.",
                        Chapters =
                        [
                            new StudyChapter
                            {
                                Id = "chap-intro",
                                SubjectId = "subj-algorithms",
                                Title = "Chapter 1: Foundational Paradigms",
                                Order = 1,
                                Lessons =
                                [
                                    new StudyLesson
                                    {
                                        Id = "lesson-asymptotic",
                                        ChapterId = "chap-intro",
                                        Title = "Asymptotic Analysis and Big-O",
                                        Summary = "Understanding time and space complexity metrics.",
                                        Type = LessonType.Concept,
                                        EstimatedMinutes = 20,
                                        Order = 1
                                    },
                                    new StudyLesson
                                    {
                                        Id = "lesson-recursion",
                                        ChapterId = "chap-intro",
                                        Title = "Recursion & Divide and Conquer",
                                        Summary = "Mastering recursive call trees and master theorem analysis.",
                                        Type = LessonType.Practice,
                                        EstimatedMinutes = 30,
                                        Order = 2
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };

            var tempPath = coursesPath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, new List<StudyCourse> { sampleCourse }, JsonOptions, ct);
            }
            File.Move(tempPath, coursesPath, overwrite: true);
        }

        var sessionsPath = Path.Combine(defaultUserDir, "sessions.json");
        if (!File.Exists(sessionsPath))
        {
            var tempPath = sessionsPath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, new List<StudySession>(), JsonOptions, ct);
            }
            File.Move(tempPath, sessionsPath, overwrite: true);
        }

        var progressPath = Path.Combine(defaultUserDir, "progress.json");
        if (!File.Exists(progressPath))
        {
            var tempPath = progressPath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, new List<StudyProgress>(), JsonOptions, ct);
            }
            File.Move(tempPath, progressPath, overwrite: true);
        }
    }

    private async Task SaveAppliedMigrationsAsync(List<SchemaVersionRecord> migrations, CancellationToken ct)
    {
        var path = GetVersionFilePath();
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, migrations, JsonOptions, ct);
        }
        File.Move(temp, path, overwrite: true);
    }
}
