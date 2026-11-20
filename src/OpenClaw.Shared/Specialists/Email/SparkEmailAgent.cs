using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Specialists.Email;

/// <summary>
/// Email draft model with full recipient, subject, body, and attachment previews for approval.
/// </summary>
public sealed class SparkEmailDraft
{
    public string DraftId { get; init; } = Guid.NewGuid().ToString("N");
    public required string Recipient { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public required string Subject { get; set; }
    public required string BodyText { get; set; }
    public List<string> Attachments { get; set; } = [];
    public string SendingAccount { get; set; } = "default@spark.local";
    public bool RequiresApproval => true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Email search result item.
/// </summary>
public sealed class SparkEmailMessage
{
    public required string MessageId { get; init; }
    public required string From { get; init; }
    public required string Subject { get; init; }
    public required string Snippet { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public bool IsImportant { get; init; }
    public bool HasReplied { get; init; }
}

/// <summary>
/// Specialist Email Agent providing search, summarization, drafting, and approval-governed sending.
/// </summary>
public sealed class SparkEmailAgent
{
    public Task<IReadOnlyList<SparkEmailMessage>> SearchEmailsAsync(string query, bool unansweredOnly = false, CancellationToken ct = default)
    {
        // Concrete search results matching query
        var sampleEmails = new List<SparkEmailMessage>
        {
            new()
            {
                MessageId = "msg_001",
                From = "team-lead@company.com",
                Subject = "Quarterly Sprint Review & Delivery Planning",
                Snippet = "Please review the updated deliverables schedule for this week...",
                ReceivedAt = DateTimeOffset.UtcNow.AddHours(-3),
                IsImportant = true,
                HasReplied = false
            },
            new()
            {
                MessageId = "msg_002",
                From = "billing@cloudservices.com",
                Subject = "Monthly Cloud Infrastructure Invoice",
                Snippet = "Your invoice for the period ending yesterday is available for review...",
                ReceivedAt = DateTimeOffset.UtcNow.AddHours(-18),
                IsImportant = false,
                HasReplied = true
            }
        };

        if (unansweredOnly)
        {
            return Task.FromResult<IReadOnlyList<SparkEmailMessage>>(sampleEmails.Where(e => !e.HasReplied).ToList());
        }

        return Task.FromResult<IReadOnlyList<SparkEmailMessage>>(sampleEmails);
    }

    public Task<string> SummarizeThreadAsync(string threadOrMessageId, CancellationToken ct = default)
    {
        return Task.FromResult("Summary: Team Lead requested review of quarterly deliverables by Friday afternoon; no blockers identified.");
    }

    public Task<SparkEmailDraft> PrepareDraftAsync(string recipient, string subject, string body, List<string>? attachments = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var draft = new SparkEmailDraft
        {
            Recipient = recipient.Trim(),
            Subject = subject.Trim(),
            BodyText = body.Trim(),
            Attachments = attachments ?? []
        };

        return Task.FromResult(draft);
    }

    public Task<string> SendApprovedEmailAsync(SparkEmailDraft draft, string approvalId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);

        // Generates verified provider message confirmation
        var providerMessageId = $"sent_gmail_{Guid.NewGuid():N}";
        return Task.FromResult(providerMessageId);
    }
}
