using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Orchestration;

/// <summary>
/// Compiles educational context by uniting course syllabus metadata and episodic
/// memories from SPARK's unified memory engine.
/// </summary>
public sealed class StudyContextProvider : IStudyContextProvider
{
    private readonly IStudyDataStore _dataStore;
    private readonly ISparkMemoryStore? _memoryStore;

    public StudyContextProvider(IStudyDataStore dataStore, ISparkMemoryStore? memoryStore = null)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _memoryStore = memoryStore;
    }

    public async Task<StudyContextEnvelope> BuildContextAsync(
        string query,
        string userId = "default",
        string? courseId = null,
        string? lessonId = null,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var retrievedMemories = new List<string>();
        var syllabusSnippet = string.Empty;

        // 1. Course & Lesson Taxonomy
        if (!string.IsNullOrWhiteSpace(courseId))
        {
            var course = await _dataStore.GetCourseAsync(courseId, userId, ct);
            if (course != null)
            {
                sb.AppendLine($"[Current Course]: {course.Title} (Level: {course.Level})");
                if (!string.IsNullOrWhiteSpace(course.Description))
                {
                    sb.AppendLine($"[Course Description]: {course.Description}");
                }

                if (!string.IsNullOrWhiteSpace(lessonId))
                {
                    var lesson = course.Subjects
                        .SelectMany(s => s.Chapters)
                        .SelectMany(c => c.Lessons)
                        .FirstOrDefault(l => string.Equals(l.Id, lessonId, StringComparison.OrdinalIgnoreCase));

                    if (lesson != null)
                    {
                        syllabusSnippet = $"Lesson: {lesson.Title} ({lesson.Type}) - {lesson.Summary}";
                        sb.AppendLine($"[Active Lesson]: {syllabusSnippet}");
                    }
                }
            }
        }

        // 2. Query SPARK Memory Store for relevant prior learning notes/insights
        if (_memoryStore != null && !string.IsNullOrWhiteSpace(query))
        {
            try
            {
                var searchFilter = new MemoryFilter
                {
                    RequiredTags = ["study", "education", "concept"],
                    Scope = MemoryScope.Semantic
                };

                var results = await _memoryStore.SearchAsync(
                    query,
                    filter: searchFilter,
                    limit: 3,
                    userId: userId,
                    ct: ct);

                foreach (var res in results)
                {
                    if (res.Record?.Content != null)
                    {
                        retrievedMemories.Add(res.Record.Content);
                        sb.AppendLine($"[Relevant Memory]: {res.Record.Content}");
                    }
                }
            }
            catch
            {
                // Graceful fallback if memory search encounters errors
            }
        }

        return new StudyContextEnvelope(
            FormattedContext: sb.ToString().Trim(),
            RetrievedMemorySnippets: retrievedMemories,
            ActiveSyllabusSnippet: syllabusSnippet);
    }
}
