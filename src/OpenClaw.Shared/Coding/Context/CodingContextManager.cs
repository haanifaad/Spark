using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Context;

/// <summary>
/// Manages conversation token budgeting and performs automatic compaction
/// when conversation history nears model context limits.
/// </summary>
public sealed class CodingContextManager
{
    public const int DefaultContextLimitTokens = 32000;
    public const int BufferReservePercent = 15; // Keep 15% buffer free
    public const int KeepRecentTurns = 4; // Keep 4 recent turns verbatim

    public int ContextLimitTokens { get; set; } = DefaultContextLimitTokens;

    /// <summary>
    /// Estimates token count of a string (approx 4 characters per token).
    /// </summary>
    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    /// <summary>
    /// Calculates total estimated tokens in a coding session's message history.
    /// </summary>
    public static int CalculateHistoryTokens(IEnumerable<CodingMessage> history)
    {
        return history.Sum(m => EstimateTokens(m.Content) + (m.CommandResult != null ? EstimateTokens(m.CommandResult.CombinedOutput) : 0));
    }

    /// <summary>
    /// Checks whether compaction is required based on current token usage.
    /// </summary>
    public bool ShouldCompact(IEnumerable<CodingMessage> history)
    {
        int tokens = CalculateHistoryTokens(history);
        int threshold = (int)(ContextLimitTokens * (1.0 - (BufferReservePercent / 100.0)));
        return tokens >= threshold;
    }

    /// <summary>
    /// Compacts conversation history by summarizing earlier turns and retaining recent turns verbatim.
    /// </summary>
    public List<CodingMessage> CompactHistory(List<CodingMessage> history, CodingPlan? activePlan)
    {
        if (history.Count <= KeepRecentTurns + 1)
        {
            return new List<CodingMessage>(history);
        }

        int splitIndex = history.Count - KeepRecentTurns;
        var earlierTurns = history.Take(splitIndex).ToList();
        var recentTurns = history.Skip(splitIndex).ToList();

        // Generate structured compaction summary
        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine("[COMPACTED CONVERSATION SUMMARY]");
        summaryBuilder.AppendLine($"Compacted turns: {earlierTurns.Count}");

        // Record goals and decisions
        var userRequests = earlierTurns.Where(m => m.Role == "user").Select(m => m.Content).Take(3);
        summaryBuilder.AppendLine($"Original Request(s): {string.Join("; ", userRequests)}");

        // Record touched files
        var touchedFiles = earlierTurns
            .Where(m => m.FileChanges != null)
            .SelectMany(m => m.FileChanges!)
            .Select(c => c.FilePath)
            .Distinct()
            .ToList();

        if (touchedFiles.Count > 0)
        {
            summaryBuilder.AppendLine($"Files Modified: {string.Join(", ", touchedFiles)}");
        }

        // Record commands executed
        var executedCommands = earlierTurns
            .Where(m => m.CommandResult != null)
            .Select(m => $"{m.CommandResult!.Command} (exit: {m.CommandResult.ExitCode})")
            .Distinct()
            .ToList();

        if (executedCommands.Count > 0)
        {
            summaryBuilder.AppendLine($"Commands Run: {string.Join("; ", executedCommands.Take(5))}");
        }

        // Active plan progress
        if (activePlan != null)
        {
            summaryBuilder.AppendLine($"Plan Status: {activePlan.CompletedStepCount}/{activePlan.TotalStepCount} steps completed.");
            foreach (var step in activePlan.Steps.Where(s => s.Status == PlanStepStatus.Completed))
            {
                summaryBuilder.AppendLine($"  - [Done] {step.Title}");
            }
            var inProgress = activePlan.Steps.FirstOrDefault(s => s.Status == PlanStepStatus.InProgress);
            if (inProgress != null)
            {
                summaryBuilder.AppendLine($"  - [In Progress] {inProgress.Title}");
            }
        }

        var compactedTurn = new CodingMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Role = "system",
            Content = summaryBuilder.ToString().Trim(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var result = new List<CodingMessage> { compactedTurn };
        result.AddRange(recentTurns);

        return result;
    }
}
