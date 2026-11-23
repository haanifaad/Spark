using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Tools;

/// <summary>
/// Flashcard generator that synthesizes study flashcards from concepts and lesson text.
/// </summary>
public sealed class FlashcardGeneratorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("topic", out var topicObj) || topicObj is not string topic || string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Missing parameter 'topic'.");
        }

        string? content = arguments.TryGetValue("content", out var c) ? c?.ToString() : null;
        int count = 5;
        if (arguments.TryGetValue("count", out var cntObj) && cntObj != null)
        {
            count = Math.Clamp(Convert.ToInt32(cntObj), 1, 20);
        }

        var cards = GenerateFlashcards(topic, content, count);

        return Task.FromResult<object?>(new
        {
            topic,
            totalCards = cards.Count,
            flashcards = cards
        });
    }

    private static List<object> GenerateFlashcards(string topic, string? content, int count)
    {
        var list = new List<object>();

        // If content provides definitions or lines
        if (!string.IsNullOrWhiteSpace(content))
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (list.Count >= count) break;
                var trimmed = line.Trim();
                if (trimmed.Contains(':'))
                {
                    var parts = trimmed.Split(new[] { ':' }, 2);
                    list.Add(new
                    {
                        id = $"card-{list.Count + 1}",
                        front = $"What is {parts[0].Trim()}?",
                        back = parts[1].Trim(),
                        difficulty = "Medium",
                        topic
                    });
                }
                else if (trimmed.Contains(" - "))
                {
                    var parts = trimmed.Split(new[] { " - " }, 2, StringSplitOptions.None);
                    list.Add(new
                    {
                        id = $"card-{list.Count + 1}",
                        front = $"Define {parts[0].Trim()}",
                        back = parts[1].Trim(),
                        difficulty = "Easy",
                        topic
                    });
                }
            }
        }

        // Fill remaining cards with structured pedagogical cards for the topic
        (string, string, string)[] standardAspects = [
            ("Core Concept", $"What is the fundamental definition of {topic}?", $"{topic} is a foundational subject in this curriculum."),
            ("Key Application", $"Where is {topic} most prominently applied in practice?", $"{topic} is utilized in real-world technical and academic systems."),
            ("Underlying Mechanism", $"How does {topic} operate under standard operational conditions?", $"{topic} functions via structured rules, algorithms, or axiomatic steps."),
            ("Common Pitfalls", $"What is a common error or misconception when working with {topic}?", $"Overlooking boundary conditions, edge cases, or theoretical assumptions."),
            ("Best Practice", $"What is the recommended approach to master {topic}?", $"Break down the principles step by step, apply deliberate practice, and verify outcomes.")
        ];

        for (int i = 0; list.Count < count && i < standardAspects.Length; i++)
        {
            var (aspect, q, a) = standardAspects[i];
            list.Add(new
            {
                id = $"card-{list.Count + 1}",
                aspect,
                front = q,
                back = a,
                difficulty = i > 2 ? "Hard" : "Medium",
                topic
            });
        }

        return list;
    }
}

/// <summary>
/// Dynamic quiz generator generating multiple choice and review questions.
/// </summary>
public sealed class QuizGeneratorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("topic", out var topObj) || topObj is not string topic || string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Missing parameter 'topic'.");
        }

        int questionsCount = 3;
        if (arguments.TryGetValue("questions", out var qCountObj) && qCountObj != null)
        {
            questionsCount = Math.Clamp(Convert.ToInt32(qCountObj), 1, 10);
        }

        var questions = new List<object>();
        for (int i = 1; i <= questionsCount; i++)
        {
            questions.Add(new
            {
                questionNumber = i,
                question = $"Which of the following statements accurately characterizes {topic} in an educational context? (Question {i})",
                options = new[]
                {
                    $"A) {topic} represents a structured domain with defined principles and verifiable criteria.",
                    $"B) {topic} has no measurable outcomes or theoretical underpinnings.",
                    $"C) {topic} is only applicable in single-threaded environments.",
                    $"D) None of the above."
                },
                correctAnswer = "A",
                explanation = $"Statement A is correct because {topic} adheres to pedagogical and scientific principles.",
                bloomLevel = i switch { 1 => "Remembering", 2 => "Understanding", _ => "Applying" }
            });
        }

        return Task.FromResult<object?>(new
        {
            topic,
            totalQuestions = questions.Count,
            quiz = questions
        });
    }
}

/// <summary>
/// Reads real student progress from the Study DataStore.
/// </summary>
public sealed class StudyProgressReaderTool : IStudyToolHandler
{
    private readonly IStudyDataStore _dataStore;

    public StudyProgressReaderTool(IStudyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        var courseId = arguments.TryGetValue("courseId", out var c) ? c?.ToString() : "course-foundation-cs";
        var userId = arguments.TryGetValue("userId", out var u) ? u?.ToString() : "default";

        if (string.IsNullOrWhiteSpace(courseId))
        {
            courseId = "course-foundation-cs";
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "default";
        }

        var progress = await _dataStore.GetProgressAsync(courseId, userId, ct);
        var courses = await _dataStore.ListCoursesAsync(userId, ct);
        var activeCourse = courses.FirstOrDefault(x => string.Equals(x.Id, courseId, StringComparison.OrdinalIgnoreCase));

        return new
        {
            userId,
            courseId,
            courseTitle = activeCourse?.Title ?? courseId,
            completionPercentage = progress.CompletionPercentage,
            completedLessonsCount = progress.CompletedLessonIds.Count,
            completedLessons = progress.CompletedLessonIds,
            activeLessonId = progress.ActiveLessonId,
            streakDays = progress.StreakDays,
            lastActivityUtc = progress.LastActivityUtc
        };
    }
}

/// <summary>
/// Reads real learning objectives for a lesson or topic from the Study DataStore.
/// </summary>
public sealed class LearningObjectiveReaderTool : IStudyToolHandler
{
    private readonly IStudyDataStore _dataStore;

    public LearningObjectiveReaderTool(IStudyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        var lessonId = arguments.TryGetValue("lessonId", out var l) ? l?.ToString() : null;
        var userId = arguments.TryGetValue("userId", out var u) ? u?.ToString() : "default";

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "default";
        }

        var objectives = await _dataStore.ListObjectivesAsync(lessonId, userId, ct);

        return new
        {
            lessonId = lessonId ?? "all-active",
            userId,
            totalObjectives = objectives.Count,
            masteredCount = objectives.Count(o => o.IsMastered),
            masteryPercentage = objectives.Count > 0 ? (int)((objectives.Count(o => o.IsMastered) / (double)objectives.Count) * 100) : 0,
            objectives = objectives.Select(o => new
            {
                id = o.Id,
                description = o.Description,
                taxonomyLevel = o.TaxonomyLevel.ToString(),
                isMastered = o.IsMastered,
                orderIndex = o.OrderIndex
            })
        };
    }
}
