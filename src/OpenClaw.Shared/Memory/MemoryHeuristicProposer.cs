using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Watchdog;

namespace OpenClaw.Shared.Memory;

public sealed record ProposedMemoryItem
{
    public required string Id { get; init; }
    public required string ProposedFact { get; init; }
    public required string SourceText { get; init; }
    public MemoryCategory Category { get; init; } = MemoryCategory.Preference;
    public DateTimeOffset ProposedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsDecided { get; set; }
    public bool IsAccepted { get; set; }
}

/// <summary>
/// Zero-token heuristic memory proposer.
/// Extracts enduring user preferences, constraints, and facts from idle conversations
/// without burning expensive LLM tokens, queuing them for user review.
/// </summary>
public sealed class MemoryHeuristicProposer
{
    private static readonly Regex[] LastingFactPatterns =
    [
        new(@"\b(?:please\s+)?remember(?:\s+that)?\s*[:,]?\s+([^.!?\n]{6,})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?:from now on|going forward|in the future)\s*,?\s+([^.!?\n]{6,})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(I\s+(?:always|never|usually|prefer|like|love|hate|dislike)\b[^.!?\n]{3,})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(my\s+[a-zA-Z0-9_\- ]{1,25}\s+(?:is|are)\s+[^.!?\n]{2,})", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    private static readonly Regex QuestionRegex = new(
        @"^\s*(?:do|does|did|can|could|would|will|should|is|are|what|why|how|when|where|who)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, ProposedMemoryItem> _proposals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISparkMemoryStore? _memoryStore;

    public MemoryHeuristicProposer(ISparkMemoryStore? memoryStore = null)
    {
        _memoryStore = memoryStore;
    }

    /// <summary>
    /// Scans a user message for enduring preferences or instructions.
    /// </summary>
    public IReadOnlyList<ProposedMemoryItem> ScanUserMessage(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return Array.Empty<ProposedMemoryItem>();

        var trimmed = userMessage.Trim();

        // Questions are not statements of fact or preference
        if (QuestionRegex.IsMatch(trimmed))
            return Array.Empty<ProposedMemoryItem>();

        var discovered = new List<ProposedMemoryItem>();

        foreach (var pattern in LastingFactPatterns)
        {
            var match = pattern.Match(trimmed);
            if (match.Success && match.Groups.Count > 1)
            {
                var candidate = match.Groups[1].Value.Trim();
                var clean = TurnWatchdogMonitor.RedactSecrets(candidate);

                // Omit candidates that held secrets or are too short, or already discovered
                if (clean.Contains("[REDACTED_SECRET]") || clean.Length < 6 ||
                    discovered.Any(d => string.Equals(d.ProposedFact, clean, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var proposal = new ProposedMemoryItem
                {
                    Id = $"prop_{Guid.NewGuid():N}",
                    ProposedFact = clean,
                    SourceText = trimmed.Length > 200 ? trimmed[..200] + "..." : trimmed,
                    Category = Categorize(clean)
                };

                _proposals[proposal.Id] = proposal;
                discovered.Add(proposal);
            }
        }

        return discovered;
    }

    public IReadOnlyList<ProposedMemoryItem> GetPendingProposals() =>
        _proposals.Values.Where(p => !p.IsDecided).ToList();

    public async Task<bool> AcceptProposalAsync(string proposalId, string userId = "default", CancellationToken ct = default)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal) || proposal.IsDecided)
            return false;

        proposal.IsDecided = true;
        proposal.IsAccepted = true;

        if (_memoryStore != null)
        {
            var record = new MemoryRecord
            {
                Id = $"mem_{Guid.NewGuid():N}",
                UserId = userId,
                Content = proposal.ProposedFact,
                Category = proposal.Category,
                Scope = MemoryScope.UserProfile,
                Provenance = "HeuristicProposer:Accepted"
            };

            await _memoryStore.RememberAsync(record, ct).ConfigureAwait(false);
        }

        return true;
    }

    public bool RejectProposal(string proposalId)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal) || proposal.IsDecided)
            return false;

        proposal.IsDecided = true;
        proposal.IsAccepted = false;
        return true;
    }

    private static MemoryCategory Categorize(string text)
    {
        if (text.Contains("prefer", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("like", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("always", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("never", StringComparison.OrdinalIgnoreCase))
        {
            return MemoryCategory.Preference;
        }

        return MemoryCategory.GeneralFact;
    }
}
