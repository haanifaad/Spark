using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Orchestration;

/// <summary>
/// Educational AI Orchestrator implementing intent classification, context aggregation,
/// permission evaluation, and SPARK model routing.
/// </summary>
public sealed class StudyOrchestrator : IStudyOrchestrator
{
    private readonly IStudyIntentRouter _intentRouter;
    private readonly IStudyContextProvider _contextProvider;
    private readonly ISparkModelRouter? _modelRouter;
    private readonly IStudyPermissionManager? _permissionManager;
    private readonly IStudyEventBus? _eventBus;

    public StudyOrchestrator(
        IStudyIntentRouter intentRouter,
        IStudyContextProvider contextProvider,
        ISparkModelRouter? modelRouter = null,
        IStudyPermissionManager? permissionManager = null,
        IStudyEventBus? eventBus = null)
    {
        _intentRouter = intentRouter ?? throw new ArgumentNullException(nameof(intentRouter));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _modelRouter = modelRouter;
        _permissionManager = permissionManager;
        _eventBus = eventBus;
    }

    public async Task<StudyInteractionResult> ProcessTurnAsync(
        StudyInteractionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();

        // 1. Evaluate intent
        var intentDecision = _intentRouter.Classify(request.Query);

        // 2. Build multi-tier educational context
        var context = await _contextProvider.BuildContextAsync(
            request.Query,
            request.UserId,
            request.CourseId,
            request.LessonId,
            ct);

        // 3. Permission authorization check if requesting local data or tool execution
        if (_permissionManager != null)
        {
            var authDecision = await _permissionManager.AuthorizeAsync(
                StudyPermissionKind.ReadOnly,
                $"Study.Turn.{intentDecision.PrimaryIntent}",
                $"Processing study turn for {intentDecision.PrimaryIntent}",
                request.UserId,
                ct);

            if (!authDecision.IsAllowed)
            {
                stopwatch.Stop();
                return new StudyInteractionResult(
                    ResponseText: "Execution blocked by Study Division security policy: " + (authDecision.Reason ?? "Access denied."),
                    IdentifiedIntent: intentDecision.PrimaryIntent,
                    ModelUsed: "None",
                    IsFallback: false,
                    LatencyMs: stopwatch.ElapsedMilliseconds);
            }
        }

        // 4. Map educational intent to SPARK model routing request
        var taskCategory = intentDecision.PrimaryIntent switch
        {
            StudyEducationalIntent.ProblemSolvingPractice => SparkTaskCategory.Mathematics,
            StudyEducationalIntent.SummaryAndSynthesis => SparkTaskCategory.DocumentAnalysis,
            StudyEducationalIntent.ExamReview => SparkTaskCategory.ComplexReasoning,
            StudyEducationalIntent.SocraticTutoring => SparkTaskCategory.ComplexReasoning,
            _ => SparkTaskCategory.SimpleConversation
        };

        var routingRequest = new SparkRoutingRequest
        {
            TaskCategory = taskCategory,
            PrivacyMode = request.EnforceLocalOnly ? SparkModelPrivacyMode.StrictLocalOnly : SparkModelPrivacyMode.Balanced,
            ContainsSensitiveLocalData = request.EnforceLocalOnly
        };

        string modelName = "spark-study-default";
        bool wasFallback = false;
        string generatedContent = string.Empty;

        // 5. Model execution via SPARK Model Router (if configured) or resilient local synthesis
        if (_modelRouter != null)
        {
            try
            {
                var routingDecision = _modelRouter.Route(routingRequest);
                modelName = routingDecision.SelectedModel?.ModelId ?? "spark-routed-model";

                var inferenceResult = await _modelRouter.ExecuteWithFallbackAsync(
                    routingRequest,
                    async (descriptor, cancellation) =>
                    {
                        // In Phase 1 foundation: prompt envelope synthesis
                        await Task.Delay(10, cancellation);
                        return $"[Study Division Tutor - {intentDecision.PrimaryIntent}]\n" +
                               $"{FormatEducationalResponse(intentDecision.PrimaryIntent, request.Query, context)}";
                    },
                    ct);

                if (inferenceResult.Success && !string.IsNullOrWhiteSpace(inferenceResult.Content))
                {
                    generatedContent = inferenceResult.Content;
                    modelName = inferenceResult.ModelUsed;
                    wasFallback = inferenceResult.WasFallbackUsed;
                }
            }
            catch (Exception ex)
            {
                wasFallback = true;
                generatedContent = $"[Study Division Offline Fallback]\n" +
                                   FormatEducationalResponse(intentDecision.PrimaryIntent, request.Query, context) +
                                   $"\n\n*(Note: Inference routed through resilient fallback due to: {ex.Message})*";
            }
        }

        if (string.IsNullOrWhiteSpace(generatedContent))
        {
            generatedContent = $"[Study Division AI - {intentDecision.PrimaryIntent}]\n" +
                               FormatEducationalResponse(intentDecision.PrimaryIntent, request.Query, context);
        }

        // 6. Publish event if bus available
        if (_eventBus != null && !string.IsNullOrWhiteSpace(request.SessionId))
        {
            await _eventBus.PublishAsync(
                new StudyMessageReceivedEvent(request.SessionId, generatedContent, "Tutor"),
                ct);
        }

        stopwatch.Stop();
        return new StudyInteractionResult(
            ResponseText: generatedContent,
            IdentifiedIntent: intentDecision.PrimaryIntent,
            ModelUsed: modelName,
            IsFallback: wasFallback,
            LatencyMs: stopwatch.ElapsedMilliseconds);
    }

    private static string FormatEducationalResponse(
        StudyEducationalIntent intent,
        string query,
        StudyContextEnvelope context)
    {
        var preamble = string.IsNullOrWhiteSpace(context.ActiveSyllabusSnippet)
            ? string.Empty
            : $"Regarding {context.ActiveSyllabusSnippet}:\n\n";

        return intent switch
        {
            StudyEducationalIntent.SocraticTutoring =>
                $"{preamble}That's an insightful question about \"{query}\". Before diving in, what is your initial intuition about what causes this phenomenon? What principles have we explored so far that might connect to it?",

            StudyEducationalIntent.ProblemSolvingPractice =>
                $"{preamble}Let's break down this problem systematically:\n1. **Identify the Given Information**: What variables are explicitly provided?\n2. **Target Objective**: What exact quantity or outcome are we solving for?\n3. **Governing Principles**: Which theorem or law applies here?",

            StudyEducationalIntent.SummaryAndSynthesis =>
                $"{preamble}### Core Summary: {query}\n- **Key Concept**: Primary definition and fundamental mechanics.\n- **Significance**: Why this matters in the broader discipline.\n- **Application**: Practical situations where this principle is applied.",

            StudyEducationalIntent.ExamReview =>
                $"{preamble}### Rapid Exam Drill: {query}\n**Question**: In your own words, state the core mechanism of this concept and distinguish it from its closest related counterpart.\n*(Take a moment to formulate your answer before moving to the solution)*.",

            StudyEducationalIntent.ReferenceLookup =>
                $"{preamble}**Definition / Formula**: {query} refers to the foundational principle and mathematical relation defining this behavior.",

            _ =>
                $"{preamble}Here is a structured conceptual breakdown of **{query}**:\n\n1. **Core Concept**: Fundamental definition and core ideas.\n2. **Intuitive Analogy**: How this relates to everyday physical or logical systems.\n3. **Detailed Mechanics**: How the underlying components interact.\n4. **Key Takeaway**: The primary insight to retain."
        };
    }
}
