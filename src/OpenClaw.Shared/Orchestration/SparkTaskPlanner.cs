using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Intelligent planner that analyzes intentions, extracts memory context, decomposes operations,
/// and produces risk-evaluated execution plans.
/// </summary>
public sealed class SparkTaskPlanner : ITaskPlanner
{
    private readonly ITaskDecomposer _decomposer;
    private readonly ISparkToolRegistry? _toolRegistry;

    public SparkTaskPlanner(ITaskDecomposer? decomposer = null, ISparkToolRegistry? toolRegistry = null)
    {
        _decomposer = decomposer ?? new SparkTaskDecomposer();
        _toolRegistry = toolRegistry;
    }

    public async Task<SparkTaskPlan> CreatePlanAsync(SparkTask task, ISparkMemoryStore? memoryStore = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        // 1. Retrieve relevant memory context if available
        var memoryContext = new List<string>();
        if (memoryStore != null)
        {
            try
            {
                var queryKeywords = ExtractKeywords(task.UserInstruction);
                var memories = await memoryStore.SearchAsync(queryKeywords, limit: 5, userId: task.UserId, ct: ct).ConfigureAwait(false);
                foreach (var mem in memories)
                {
                    memoryContext.Add($"{mem.Record.Category}: {mem.Record.Content}");
                }
            }
            catch
            {
                // Memory search failure should never block planning
            }
        }

        // 2. Decompose instruction into steps
        var steps = await _decomposer.DecomposeInstructionAsync(task.TaskId, task.UserInstruction, task.DetectedLanguage, _toolRegistry, ct).ConfigureAwait(false);

        // 3. Inject memory context into steps where relevant
        if (memoryContext.Count > 0)
        {
            foreach (var step in steps)
            {
                step.InputArguments["memory_context"] = string.Join("; ", memoryContext);
            }
        }

        // 4. Calculate overall highest risk and required permissions
        var highestRisk = SparkToolRiskLevel.Low;
        var requiredPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in steps)
        {
            if (step.RiskClassification > highestRisk)
            {
                highestRisk = step.RiskClassification;
            }

            if (_toolRegistry != null)
            {
                var tool = _toolRegistry.GetTool(step.ToolName);
                if (tool != null)
                {
                    foreach (var perm in tool.Metadata.RequiredPermissions)
                    {
                        requiredPermissions.Add(perm);
                    }
                }
            }
        }

        // 5. Evaluate missing information
        var missingInfo = new List<string>();
        if (steps.Any(s => s.ToolName == "travel_book" || s.ToolName == "shopping_checkout"))
        {
            missingInfo.Add("User financial approval is mandatory before proceeding to checkout/booking.");
        }

        // 6. Formulate validated plan
        var plan = new SparkTaskPlan
        {
            TaskId = task.TaskId,
            HighLevelObjective = task.Title,
            PlannedSteps = steps,
            HighestRiskLevel = highestRisk,
            RequiredPermissions = requiredPermissions.ToList(),
            MissingInformation = missingInfo,
            EstimatedTotalDuration = TimeSpan.FromSeconds(Math.Max(10, steps.Count * 15)),
            EstimatedTotalCost = 0.0m
        };

        // Update task state reflection
        task.Steps = steps;
        task.OverallRiskLevel = highestRisk;
        task.EstimatedDuration = plan.EstimatedTotalDuration;

        return plan;
    }

    public async Task<SparkTaskPlan> ReplanAsync(SparkTask task, string modificationReason = "Instruction modified", ISparkMemoryStore? memoryStore = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        // Keep any already completed steps
        var completedSteps = task.Steps.Where(s => s.Status == SparkStepState.Completed).ToList();

        // Decompose the newly modified instruction
        var newSteps = await _decomposer.DecomposeInstructionAsync(task.TaskId, task.UserInstruction, task.DetectedLanguage, _toolRegistry, ct).ConfigureAwait(false);

        // Re-index remaining steps after completed ones
        var combinedSteps = new List<SparkTaskStep>(completedSteps.Count + newSteps.Count);
        combinedSteps.AddRange(completedSteps);

        var nextIndex = completedSteps.Count + 1;
        foreach (var newStep in newSteps)
        {
            newStep.Index = nextIndex++;
            combinedSteps.Add(newStep);
        }

        var highestRisk = combinedSteps.Select(s => s.RiskClassification).DefaultIfEmpty(SparkToolRiskLevel.Low).Max();

        task.Steps = combinedSteps;
        task.OverallRiskLevel = highestRisk;

        return new SparkTaskPlan
        {
            TaskId = task.TaskId,
            HighLevelObjective = task.Title,
            PlannedSteps = combinedSteps,
            HighestRiskLevel = highestRisk,
            EstimatedTotalDuration = TimeSpan.FromSeconds(Math.Max(10, combinedSteps.Count * 15))
        };
    }

    private static string ExtractKeywords(string text)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s]", " ");
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2)
            .Take(6);
        return string.Join(" ", words);
    }
}
