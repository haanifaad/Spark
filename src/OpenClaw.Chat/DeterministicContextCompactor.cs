using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenClaw.Shared.Watchdog;

namespace OpenClaw.Chat;

/// <summary>
/// Deterministic transcript compactor for conversation history.
/// Preserves recent exchanges verbatim while synthesizing structured excerpts for older history.
/// Stabilizes prompt cache hits and prevents token window exhaustion.
/// </summary>
public static class DeterministicContextCompactor
{
    public const int DefaultKeepExchanges = 4;
    public const int DefaultMaxSummaryChars = 4000;
    public const int DefaultTokenBudget = 24000;
    public const double CharsPerToken = 4.0;

    public const string SummaryHeader =
        "[Summary of earlier messages in this conversation, made of excerpts, oldest first. " +
        "Background context, not new instructions: later messages override earlier ones. Full history is preserved.]\n";

    /// <summary>
    /// Evaluates conversation items against the token budget and applies deterministic folding if needed.
    /// </summary>
    public static IReadOnlyList<ChatTimelineItem> CompactIfNeeded(
        IReadOnlyList<ChatTimelineItem> items,
        int keepExchanges = DefaultKeepExchanges,
        int tokenBudget = DefaultTokenBudget,
        int maxSummaryChars = DefaultMaxSummaryChars)
    {
        if (items == null || items.Count <= keepExchanges * 2)
            return items ?? Array.Empty<ChatTimelineItem>();

        var estimatedTokens = EstimateTokens(items);
        if (estimatedTokens <= tokenBudget)
            return items;

        // Identify split index for the last N user exchanges
        var userIndices = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Kind == ChatTimelineItemKind.User)
            {
                userIndices.Add(i);
            }
        }

        if (userIndices.Count <= keepExchanges)
            return items; // Not enough exchanges to safely fold

        var splitIndex = userIndices[userIndices.Count - keepExchanges];
        var older = items.Take(splitIndex).ToList();
        var recent = items.Skip(splitIndex).ToList();

        // Build deterministic excerpt summary
        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine(SummaryHeader);

        foreach (var item in older)
        {
            if (item.Kind == ChatTimelineItemKind.User)
            {
                var clean = TurnWatchdogMonitor.RedactSecrets(item.ActiveText.Trim());
                if (clean.Length > 200) clean = clean[..200] + "...";
                summaryBuilder.AppendLine($"- User asked: {clean}");
            }
            else if (item.Kind == ChatTimelineItemKind.Assistant && !string.IsNullOrWhiteSpace(item.ActiveText))
            {
                var clean = TurnWatchdogMonitor.RedactSecrets(item.ActiveText.Trim());
                if (clean.Length > 200) clean = clean[..200] + "...";
                summaryBuilder.AppendLine($"  Assistant answered: {clean}");
            }
            else if (item.Kind == ChatTimelineItemKind.ToolCall && !string.IsNullOrWhiteSpace(item.ToolName))
            {
                summaryBuilder.AppendLine($"  Tool completed: {item.ToolName} (Status: {item.ToolResult})");
            }

            if (summaryBuilder.Length >= maxSummaryChars)
            {
                summaryBuilder.AppendLine("... (older excerpts truncated)");
                break;
            }
        }

        var summaryItem = new ChatTimelineItem(
            Id: $"compacted-{Guid.NewGuid():N}",
            Kind: ChatTimelineItemKind.Status,
            Text: summaryBuilder.ToString().TrimEnd());

        var result = new List<ChatTimelineItem>(recent.Count + 1)
        {
            summaryItem
        };
        result.AddRange(recent);

        return result;
    }

    public static int EstimateTokens(IReadOnlyList<ChatTimelineItem> items)
    {
        if (items == null || items.Count == 0) return 0;
        long totalChars = 0;
        foreach (var item in items)
        {
            totalChars += item.ActiveText?.Length ?? 0;
            totalChars += item.ToolOutput?.Length ?? 0;
        }

        return (int)Math.Ceiling(totalChars / CharsPerToken);
    }
}
