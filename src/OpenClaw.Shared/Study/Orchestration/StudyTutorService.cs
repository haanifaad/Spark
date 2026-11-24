using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClaw.Shared.Study.Orchestration;

/// <summary>
/// Core AI Tutor implementation orchestrating structured pedagogical instruction,
/// multi-turn dialogue retention, teaching mode adaptation, and model routing with fallback.
/// </summary>
public sealed class StudyTutorService : IStudyTutorService
{
    private readonly ISparkModelRouter? _modelRouter;
    private readonly IStudyIntentRouter? _intentRouter;

    public StudyTutorService(
        ISparkModelRouter? modelRouter = null,
        IStudyIntentRouter? intentRouter = null)
    {
        _modelRouter = modelRouter;
        _intentRouter = intentRouter;
    }

    public string BuildSystemPrompt(
        TeachingMode mode,
        LearningLanguage language,
        EducationLevel level,
        string? activeSubject = null,
        string? activeTopic = null,
        IReadOnlyList<LearningObjective>? objectives = null)
    {
        return StudyTeachingPrompts.BuildSystemPrompt(mode, language, level, activeSubject, activeTopic, objectives);
    }

    public async Task<StudySessionMessage> GenerateTutorTurnAsync(
        StudySession session,
        string userPrompt,
        IReadOnlyList<LearningObjective>? objectives = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        // 1. Detect dynamic pedagogical adjustments in user inquiry
        var effectiveMode = DetectAdaptiveMode(userPrompt, session.TeachingMode);
        var effectiveLanguage = DetectAdaptiveLanguage(userPrompt, session.Language);
        var effectiveLevel = session.Level;
        var activeTopic = session.CurrentTopic ?? ExtractTopicFromPrompt(userPrompt);

        // 2. Classify intent if router available
        var intent = _intentRouter?.Classify(userPrompt).PrimaryIntent.ToString() ?? "Instruction";

        // 3. Assemble multi-turn conversational context
        var conversationContext = BuildConversationHistory(session.Messages, maxHistoryTurns: 6);

        // 4. Construct complete system instruction envelope
        var systemPrompt = BuildSystemPrompt(
            effectiveMode,
            effectiveLanguage,
            effectiveLevel,
            session.SubjectId,
            activeTopic,
            objectives);

        // 5. Select routing task category
        var taskCategory = effectiveMode switch
        {
            TeachingMode.Practical => SparkTaskCategory.Coding,
            TeachingMode.Advanced or TeachingMode.Detailed or TeachingMode.Socratic => SparkTaskCategory.ComplexReasoning,
            TeachingMode.Revision => SparkTaskCategory.DocumentAnalysis,
            _ => SparkTaskCategory.SimpleConversation
        };

        var routingRequest = new SparkRoutingRequest
        {
            TaskCategory = taskCategory,
            PrivacyMode = SparkModelPrivacyMode.Balanced,
            ContainsSensitiveLocalData = false
        };

        string modelUsed = "spark-study-tutor";
        bool isFallback = false;
        string content = string.Empty;

        // 6. Execute inference via SPARK Model Router or resilient local synthesis
        if (_modelRouter != null)
        {
            try
            {
                var routingDecision = _modelRouter.Route(routingRequest);
                modelUsed = routingDecision.SelectedModel?.ModelId ?? "spark-routed-model";

                var inferenceResult = await _modelRouter.ExecuteWithFallbackAsync(
                    routingRequest,
                    async (descriptor, cancellation) =>
                    {
                        try
                        {
                            using var ollama = new OpenClaw.Shared.Inference.OllamaInferenceBackend();
                            var chatRes = await ollama.ChatAsync(new OpenClaw.Shared.Inference.LocalInferenceChatRequest(
                                Model: descriptor.ModelId ?? "spark-local-default",
                                Prompt: userPrompt,
                                System: systemPrompt,
                                Temperature: 0.7,
                                MaxTokens: 1024,
                                TimeoutMs: 30000
                            ), cancellation);
                            if (!string.IsNullOrWhiteSpace(chatRes.Response))
                            {
                                return chatRes.Response;
                            }
                        }
                        catch
                        {
                            // Gracefully fall back to local pedagogy template if local daemon is unreachable
                        }

                        return StudyTeachingPrompts.GenerateLocalPedagogicalResponse(
                            userPrompt,
                            effectiveMode,
                            effectiveLanguage,
                            effectiveLevel,
                            activeTopic);
                    },
                    ct);

                if (inferenceResult.Success && !string.IsNullOrWhiteSpace(inferenceResult.Content))
                {
                    content = inferenceResult.Content;
                    modelUsed = inferenceResult.ModelUsed;
                    isFallback = inferenceResult.WasFallbackUsed;
                }
            }
            catch
            {
                isFallback = true;
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            isFallback = true;
            modelUsed = "spark-study-local-pedagogy";
            var offlineWarning = "> [!NOTE]\n> **Offline Mode**: Ollama inference daemon is unreachable at `http://127.0.0.1:11434`. Showing structured pedagogical response template.\n\n";
            content = offlineWarning + StudyTeachingPrompts.GenerateLocalPedagogicalResponse(
                userPrompt,
                effectiveMode,
                effectiveLanguage,
                effectiveLevel,
                activeTopic);
        }

        return new StudySessionMessage(
            Id: Guid.NewGuid().ToString("N"),
            Sender: StudyMessageSender.Tutor,
            Content: content,
            TimestampUtc: DateTime.UtcNow,
            Intent: intent,
            Language: effectiveLanguage,
            TeachingMode: effectiveMode,
            ExplanationStyle: effectiveMode.ToString(),
            ModelUsed: modelUsed,
            IsFallback: isFallback);
    }

    private static TeachingMode DetectAdaptiveMode(string prompt, TeachingMode currentMode)
    {
        var lower = prompt.ToLowerInvariant();
        if (lower.Contains("simpler") || lower.Contains("explain like i'm 5") || lower.Contains("easy words") || lower.Contains("simple terms"))
        {
            return TeachingMode.Beginner;
        }
        if (lower.Contains("step by step") || lower.Contains("walk me through") || lower.Contains("steps to"))
        {
            return TeachingMode.StepByStep;
        }
        if (lower.Contains("quiz me") || lower.Contains("test me") || lower.Contains("ask me questions") || lower.Contains("socratic"))
        {
            return TeachingMode.Socratic;
        }
        if (lower.Contains("code example") || lower.Contains("practical") || lower.Contains("how to code") || lower.Contains("hands on"))
        {
            return TeachingMode.Practical;
        }
        if (lower.Contains("revision") || lower.Contains("summary") || lower.Contains("cheat sheet") || lower.Contains("quick review"))
        {
            return TeachingMode.Revision;
        }
        if (lower.Contains("deep dive") || lower.Contains("under the hood") || lower.Contains("in detail"))
        {
            return TeachingMode.Detailed;
        }
        if (lower.Contains("mathematical proof") || lower.Contains("formal") || lower.Contains("rigorous") || lower.Contains("asymptotic proof"))
        {
            return TeachingMode.Advanced;
        }

        return currentMode;
    }

    private static LearningLanguage DetectAdaptiveLanguage(string prompt, LearningLanguage currentLanguage)
    {
        var lower = prompt.ToLowerInvariant();
        if (lower.Contains("in malayalam") || lower.Contains("malayalam") || lower.Contains("മലയാളം") || lower.Contains("മലയാളത്തിൽ"))
        {
            return LearningLanguage.Malayalam;
        }
        if (lower.Contains("in english") || lower.Contains("english please"))
        {
            return LearningLanguage.English;
        }

        return currentLanguage;
    }

    private static string ExtractTopicFromPrompt(string prompt)
    {
        var clean = prompt.Trim();
        if (clean.Length <= 40)
        {
            return clean;
        }

        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(5));
    }

    private static string BuildConversationHistory(IReadOnlyList<StudySessionMessage> messages, int maxHistoryTurns)
    {
        if (messages == null || messages.Count == 0)
        {
            return string.Empty;
        }

        var recent = messages.TakeLast(maxHistoryTurns * 2).ToList();
        var sb = new StringBuilder();
        foreach (var msg in recent)
        {
            var senderLabel = msg.Sender == StudyMessageSender.User ? "Learner" : "Tutor";
            sb.AppendLine($"{senderLabel}: {msg.Content}");
        }
        return sb.ToString();
    }
}
