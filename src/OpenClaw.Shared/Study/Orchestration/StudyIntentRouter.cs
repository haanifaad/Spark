using System;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Orchestration;

/// <summary>
/// Classifies inbound student and educator queries into tailored educational intents.
/// </summary>
public sealed class StudyIntentRouter : IStudyIntentRouter
{
    public StudyIntentDecision Classify(string query, StudyIntentContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new StudyIntentDecision(
                StudyEducationalIntent.ConceptualExplanation,
                0.5,
                "Empty query defaulted to ConceptualExplanation.");
        }

        var normalized = query.ToLowerInvariant();

        // 1. Exam & Quiz Review
        if (normalized.Contains("quiz") || normalized.Contains("exam") || normalized.Contains("test me") ||
            normalized.Contains("practice questions") || normalized.Contains("flashcard"))
        {
            return new StudyIntentDecision(
                StudyEducationalIntent.ExamReview,
                0.90,
                "Detected exam/quiz drill phrasing.");
        }

        // 2. Problem Solving & Exercise Practice
        if (normalized.Contains("solve") || normalized.Contains("calculate") || normalized.Contains("homework problem") ||
            normalized.Contains("step by step") || normalized.Contains("work through this") || normalized.Contains("exercise"))
        {
            return new StudyIntentDecision(
                StudyEducationalIntent.ProblemSolvingPractice,
                0.88,
                "Detected mathematical/computational exercise solving intent.");
        }

        // 3. Summary & Synthesis
        if (normalized.Contains("summarize") || normalized.Contains("summary") || normalized.Contains("tldr") ||
            normalized.Contains("cheat sheet") || normalized.Contains("bullet points") || normalized.Contains("key takeaways"))
        {
            return new StudyIntentDecision(
                StudyEducationalIntent.SummaryAndSynthesis,
                0.92,
                "Detected synthesis and summarization request.");
        }

        // 4. Socratic Tutoring / Inquiry
        if (normalized.Contains("why is") || normalized.Contains("how come") || normalized.Contains("help me think") ||
            normalized.Contains("guide me") || normalized.Contains("hint") || normalized.Contains("don't tell me the answer"))
        {
            return new StudyIntentDecision(
                StudyEducationalIntent.SocraticTutoring,
                0.85,
                "Detected inquiry or request for guided exploration.");
        }

        // 5. Reference Lookup
        if (normalized.StartsWith("what is") || normalized.StartsWith("define") || normalized.StartsWith("meaning of") ||
            normalized.Contains("formula for") || normalized.Length < 35 && normalized.EndsWith("?"))
        {
            return new StudyIntentDecision(
                StudyEducationalIntent.ReferenceLookup,
                0.80,
                "Detected brief factual definition or formula inquiry.");
        }

        // Default: Conceptual Explanation
        return new StudyIntentDecision(
            StudyEducationalIntent.ConceptualExplanation,
            0.75,
            "Defaulted to thorough conceptual explanation.");
    }
}
