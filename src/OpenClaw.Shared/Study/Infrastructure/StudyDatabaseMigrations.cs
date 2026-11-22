using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Skills;

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

        // Migration 1.2.0: Skills, Tools & Slash Commands Schema
        if (!applied.Any(m => m.Version == "1.2.0"))
        {
            await ApplyMigration_1_2_0(ct);
            applied.Add(new SchemaVersionRecord("1.2.0", "Skills, Tools, and Slash Commands schema with built-in capability seeding.", DateTime.UtcNow));
            await SaveAppliedMigrationsAsync(applied, ct);
        }

        // Migration 1.3.0: Knowledge, Research & Study Workspace Schema
        if (!applied.Any(m => m.Version == "1.3.0"))
        {
            await ApplyMigration_1_3_0(ct);
            applied.Add(new SchemaVersionRecord("1.3.0", "Knowledge collections, documents, chunks, and research workspace persistence.", DateTime.UtcNow));
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

    private async Task ApplyMigration_1_2_0(CancellationToken ct)
    {
        var defaultUserDir = Path.Combine(_storageRoot, "default");
        Directory.CreateDirectory(defaultUserDir);

        // 1. Seed Built-In Skills
        var skillsPath = Path.Combine(defaultUserDir, "skills.json");
        if (!File.Exists(skillsPath))
        {
            var skills = StudyBuiltInSkills.GetAll().ToList();
            await AtomicWriteAsync(skillsPath, skills, ct);
        }

        // 2. Initialize Skill Executions
        var skillExecutionsPath = Path.Combine(defaultUserDir, "skill_executions.json");
        if (!File.Exists(skillExecutionsPath))
        {
            await AtomicWriteAsync(skillExecutionsPath, new List<SkillExecutionRecord>(), ct);
        }

        // 3. Initialize Tool Executions
        var toolExecutionsPath = Path.Combine(defaultUserDir, "tool_executions.json");
        if (!File.Exists(toolExecutionsPath))
        {
            await AtomicWriteAsync(toolExecutionsPath, new List<StudyToolExecutionResult>(), ct);
        }

        // 4. Initialize Command Executions
        var cmdExecutionsPath = Path.Combine(defaultUserDir, "command_executions.json");
        if (!File.Exists(cmdExecutionsPath))
        {
            await AtomicWriteAsync(cmdExecutionsPath, new List<CommandExecutionRecord>(), ct);
        }
    }

    private async Task ApplyMigration_1_3_0(CancellationToken ct)
    {
        var defaultUserDir = Path.Combine(_storageRoot, "default");
        Directory.CreateDirectory(defaultUserDir);

        // 1. Knowledge Collections
        var collectionsPath = Path.Combine(defaultUserDir, "knowledge_collections.json");
        if (!File.Exists(collectionsPath))
        {
            var initialCollection = new KnowledgeCollection(
                Id: "col-default-foundations",
                UserId: "default",
                Title: "Foundations & Reference Materials",
                Description: "Primary textbooks, academic lecture notes, and curricular documentation.",
                SubjectId: "subj-algorithms",
                CourseId: "course-foundation-cs",
                Tags: ["core", "computer-science", "algorithms"],
                DocumentIds: ["doc-algorithms-primer"],
                CreatedAtUtc: DateTime.UtcNow,
                UpdatedAtUtc: DateTime.UtcNow
            );
            await AtomicWriteAsync(collectionsPath, new List<KnowledgeCollection> { initialCollection }, ct);
        }

        // 2. Study Documents
        var documentsPath = Path.Combine(defaultUserDir, "documents.json");
        if (!File.Exists(documentsPath))
        {
            var sampleDoc = new StudyDocument(
                Id: "doc-algorithms-primer",
                UserId: "default",
                FileName: "Algorithms-And-Complexity-Primer.md",
                FilePath: "knowledge/Algorithms-And-Complexity-Primer.md",
                FileSizeBytes: 10240,
                Format: ".md",
                Title: "Algorithms & Complexity Primer",
                Author: "SPARK Academic Foundation",
                CollectionId: "col-default-foundations",
                PageCount: 5,
                CharacterCount: 4200,
                Status: DocumentProcessingStatus.Indexed,
                Sha256Checksum: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                CreatedAtUtc: DateTime.UtcNow,
                ProcessedAtUtc: DateTime.UtcNow,
                Metadata: new Dictionary<string, string> { ["domain"] = "Computer Science", ["level"] = "University" }
            );
            await AtomicWriteAsync(documentsPath, new List<StudyDocument> { sampleDoc }, ct);
        }

        // 3. Document Chunks
        var chunksPath = Path.Combine(defaultUserDir, "document_chunks.json");
        if (!File.Exists(chunksPath))
        {
            var sampleChunks = new List<DocumentChunk>
            {
                new(
                    Id: "chunk-alg-1",
                    DocumentId: "doc-algorithms-primer",
                    UserId: "default",
                    ChunkIndex: 0,
                    PageNumber: 1,
                    SectionTitle: "Asymptotic Complexity & Big O",
                    Content: "Asymptotic notation describes the limiting behavior of execution time as input size n tends to infinity. Big O represents an upper bound on worst-case computational complexity.",
                    TokenCount: 32,
                    Keywords: ["asymptotic", "big o", "complexity", "worst-case"]
                ),
                new(
                    Id: "chunk-alg-2",
                    DocumentId: "doc-algorithms-primer",
                    UserId: "default",
                    ChunkIndex: 1,
                    PageNumber: 2,
                    SectionTitle: "Divide and Conquer: QuickSort & MergeSort",
                    Content: "Divide and conquer algorithms partition a problem into smaller subproblems, solve each recursively, and recombine solutions. MergeSort achieves guaranteed O(n log n) runtime, whereas QuickSort achieves O(n log n) expected runtime with in-place partitioning.",
                    TokenCount: 45,
                    Keywords: ["divide and conquer", "quicksort", "mergesort", "log n", "partitioning"]
                )
            };
            await AtomicWriteAsync(chunksPath, sampleChunks, ct);
        }

        // 4. Research Projects
        var researchPath = Path.Combine(defaultUserDir, "research_projects.json");
        if (!File.Exists(researchPath))
        {
            var sampleProject = new ResearchProject(
                Id: "proj-ml-learning",
                UserId: "default",
                Title: "Adaptive Neural Architectures in Pedagogy",
                Description: "Investigating the intersection of multi-agent cognitive modeling and personalized learning retrieval.",
                Hypothesis: "Retrieval-augmented grounding significantly lowers hallucination rates during Socratic dialogue.",
                Status: ResearchProjectStatus.Active,
                QuestionIds: ["rq-1"],
                NoteIds: ["rn-1"],
                SourceDocumentIds: ["doc-algorithms-primer"],
                EvidenceIds: ["ev-1"],
                CreatedAtUtc: DateTime.UtcNow,
                UpdatedAtUtc: DateTime.UtcNow
            );
            await AtomicWriteAsync(researchPath, new List<ResearchProject> { sampleProject }, ct);
        }

        // 5. Research Questions
        var questionsPath = Path.Combine(defaultUserDir, "research_questions.json");
        if (!File.Exists(questionsPath))
        {
            var sampleQuestion = new ResearchQuestion(
                Id: "rq-1",
                ProjectId: "proj-ml-learning",
                UserId: "default",
                QuestionText: "How does strict source attribution affect learner trust and retention?",
                Status: ResearchQuestionStatus.Investigating,
                Findings: "Empirical studies demonstrate a 40% increase in conceptual confidence when citation provenance is explicit.",
                EvidenceCount: 1,
                CreatedAtUtc: DateTime.UtcNow
            );
            await AtomicWriteAsync(questionsPath, new List<ResearchQuestion> { sampleQuestion }, ct);
        }

        // 6. Research Notes
        var notesPath = Path.Combine(defaultUserDir, "research_notes.json");
        if (!File.Exists(notesPath))
        {
            var sampleNote = new ResearchNote(
                Id: "rn-1",
                ProjectId: "proj-ml-learning",
                UserId: "default",
                Title: "Observations on Chunk Granularity",
                Content: "Chunks sized between 200 and 400 tokens with 20% overlap yield optimal keyword and semantic recall without truncating algorithmic proofs.",
                Tags: ["methodology", "chunking", "rag"],
                RelatedDocumentId: "doc-algorithms-primer",
                CreatedAtUtc: DateTime.UtcNow,
                UpdatedAtUtc: DateTime.UtcNow
            );
            await AtomicWriteAsync(notesPath, new List<ResearchNote> { sampleNote }, ct);
        }

        // 7. Research Evidence
        var evidencePath = Path.Combine(defaultUserDir, "research_evidence.json");
        if (!File.Exists(evidencePath))
        {
            var sampleEvidence = new ResearchEvidence(
                Id: "ev-1",
                ProjectId: "proj-ml-learning",
                QuestionId: "rq-1",
                UserId: "default",
                Excerpt: "Divide and conquer algorithms partition a problem into smaller subproblems... MergeSort achieves guaranteed O(n log n) runtime.",
                DocumentId: "doc-algorithms-primer",
                DocumentTitle: "Algorithms & Complexity Primer",
                PageNumber: 2,
                CredibilityRating: "Primary Course Textbook",
                Analysis: "Confirms standard divide-and-conquer taxonomy.",
                CreatedAtUtc: DateTime.UtcNow
            );
            await AtomicWriteAsync(evidencePath, new List<ResearchEvidence> { sampleEvidence }, ct);
        }

        // 8. Research Citations
        var citationsPath = Path.Combine(defaultUserDir, "research_citations.json");
        if (!File.Exists(citationsPath))
        {
            var sampleCitation = new ResearchCitation(
                Id: "cite-1",
                ProjectId: "proj-ml-learning",
                UserId: "default",
                SourceTitle: "Introduction to Algorithms, 3rd Edition",
                Author: "Cormen, T. H., Leiserson, C. E., Rivest, R. L., Stein, C.",
                Year: 2009,
                Style: "APA",
                FormattedCitation: "Cormen, T. H., Leiserson, C. E., Rivest, R. L., & Stein, C. (2009). Introduction to Algorithms (3rd ed.). MIT Press.",
                UrlOrDoi: "https://mitpress.mit.edu/9780262033848",
                CreatedAtUtc: DateTime.UtcNow
            );
            await AtomicWriteAsync(citationsPath, new List<ResearchCitation> { sampleCitation }, ct);
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
