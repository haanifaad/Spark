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
/// Supports sequential schema evolution (1.0.0 -> 1.1.0).
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

        // Migration 1.1.0: Core Learning Schema (Learning Objectives & Learning Paths)
        if (!applied.Any(m => m.Version == "1.1.0"))
        {
            await ApplyMigration_1_1_0(ct);
            applied.Add(new SchemaVersionRecord("1.1.0", "Core Learning schema with Learning Objectives and Universal Learning Paths.", DateTime.UtcNow));
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

            await AtomicWriteAsync(coursesPath, new List<StudyCourse> { sampleCourse }, ct);
        }

        var sessionsPath = Path.Combine(defaultUserDir, "sessions.json");
        if (!File.Exists(sessionsPath))
        {
            await AtomicWriteAsync(sessionsPath, new List<StudySession>(), ct);
        }

        var progressPath = Path.Combine(defaultUserDir, "progress.json");
        if (!File.Exists(progressPath))
        {
            await AtomicWriteAsync(progressPath, new List<StudyProgress>(), ct);
        }
    }

    private async Task ApplyMigration_1_1_0(CancellationToken ct)
    {
        var defaultUserDir = Path.Combine(_storageRoot, "default");
        Directory.CreateDirectory(defaultUserDir);

        // 1. Seed Learning Objectives
        var objectivesPath = Path.Combine(defaultUserDir, "learning_objectives.json");
        if (!File.Exists(objectivesPath))
        {
            var initialObjectives = new List<LearningObjective>
            {
                new()
                {
                    Id = "obj-asymp-1",
                    LessonId = "lesson-asymptotic",
                    Description = "Define Big-O, Big-Omega, and Big-Theta asymptotic notations rigorously.",
                    TaxonomyLevel = TaxonomyLevel.Understand,
                    IsMastered = false,
                    Prerequisites = []
                },
                new()
                {
                    Id = "obj-asymp-2",
                    LessonId = "lesson-asymptotic",
                    Description = "Analyze the worst-case and average-case time complexity of nested iterative algorithms.",
                    TaxonomyLevel = TaxonomyLevel.Analyze,
                    IsMastered = false,
                    Prerequisites = ["obj-asymp-1"]
                },
                new()
                {
                    Id = "obj-recur-1",
                    LessonId = "lesson-recursion",
                    Description = "Formulate mathematical recurrence relations for divide-and-conquer algorithms.",
                    TaxonomyLevel = TaxonomyLevel.Apply,
                    IsMastered = false,
                    Prerequisites = []
                },
                new()
                {
                    Id = "obj-recur-2",
                    LessonId = "lesson-recursion",
                    Description = "Solve recurrences using the Master Theorem and recursion tree method.",
                    TaxonomyLevel = TaxonomyLevel.Apply,
                    IsMastered = false,
                    Prerequisites = ["obj-recur-1"]
                }
            };

            await AtomicWriteAsync(objectivesPath, initialObjectives, ct);
        }

        // 2. Seed Universal Learning Paths
        var pathsPath = Path.Combine(defaultUserDir, "learning_paths.json");
        if (!File.Exists(pathsPath))
        {
            var initialPaths = new List<LearningPath>
            {
                new()
                {
                    Id = "path-cs-ai-university",
                    Title = "Computer Science & Intelligent Systems Mastery",
                    Description = "Comprehensive undergraduate curriculum covering computational complexity, algorithmic design, and autonomous AI systems.",
                    Level = EducationLevel.University,
                    TargetAudience = "Undergraduate and Graduate Computer Science students and researchers.",
                    OrderedCourseIds = ["course-foundation-cs"],
                    OrderedLessonIds = ["lesson-asymptotic", "lesson-recursion"],
                    EstimatedTotalHours = 45,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Id = "path-school-foundations",
                    Title = "Secondary School Computational Thinking",
                    Description = "Foundational introduction to problem-solving, logic, and beginner algorithmic principles.",
                    Level = EducationLevel.School,
                    TargetAudience = "Secondary school students (Grades 8-10).",
                    OrderedCourseIds = ["course-foundation-cs"],
                    OrderedLessonIds = ["lesson-asymptotic"],
                    EstimatedTotalHours = 15,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Id = "path-professional-engineering",
                    Title = "Professional Software Architecture & Optimization",
                    Description = "Advanced practical engineering path focusing on high-performance algorithms, system scalability, and production trade-offs.",
                    Level = EducationLevel.Professional,
                    TargetAudience = "Working software engineers and tech leads.",
                    OrderedCourseIds = ["course-foundation-cs"],
                    OrderedLessonIds = ["lesson-asymptotic", "lesson-recursion"],
                    EstimatedTotalHours = 30,
                    CreatedAtUtc = DateTime.UtcNow
                }
            };

            await AtomicWriteAsync(pathsPath, initialPaths, ct);
        }
    }

    private static async Task AtomicWriteAsync<T>(string targetPath, T data, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, data, JsonOptions, ct);
        }
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private async Task SaveAppliedMigrationsAsync(List<SchemaVersionRecord> migrations, CancellationToken ct)
    {
        var path = GetVersionFilePath();
        await AtomicWriteAsync(path, migrations, ct);
    }
}
