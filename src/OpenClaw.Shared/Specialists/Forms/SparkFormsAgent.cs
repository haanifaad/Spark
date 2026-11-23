using OpenClaw.Shared.Memory;

namespace OpenClaw.Shared.Specialists.Forms;

/// <summary>
/// Individual field within an online or application form.
/// </summary>
public sealed class SparkFormField
{
    public required string FieldId { get; init; }
    public required string Label { get; init; }
    public string? Value { get; set; }
    public bool IsRequired { get; init; }
    public string FieldType { get; init; } = "text"; // text, email, phone, date, select
    public bool IsSensitive { get; init; }
}

/// <summary>
/// Completed form package with field values, validation state, and submission preview.
/// </summary>
public sealed class SparkFormSubmissionPreview
{
    public string FormId { get; init; } = Guid.NewGuid().ToString("N");
    public required string FormTitle { get; init; }
    public required string TargetUrl { get; init; }
    public List<SparkFormField> Fields { get; init; } = [];
    public bool IsValid => Fields.Where(f => f.IsRequired).All(f => !string.IsNullOrWhiteSpace(f.Value));
    public bool RequiresHumanApproval => true;
}

/// <summary>
/// Specialist Forms Agent that extracts fields, populates permitted information from memory,
/// validates fields, and manages approval-governed submissions.
/// </summary>
public sealed class SparkFormsAgent
{
    public async Task<SparkFormSubmissionPreview> PopulateFormAsync(
        string formTitle,
        string targetUrl,
        List<SparkFormField> fields,
        ISparkMemoryStore? memoryStore,
        string userId = "default",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fields);

        // Retrieve user profile / memory facts if available
        UserProfile? profile = null;
        if (memoryStore != null)
        {
            try
            {
                profile = await memoryStore.GetUserProfileAsync(userId, ct).ConfigureAwait(false);
            }
            catch
            {
                // Ignore profile lookup failures
            }
        }

        foreach (var field in fields)
        {
            if (!string.IsNullOrEmpty(field.Value))
                continue;

            var labelLower = field.Label.ToLowerInvariant();
            if (profile != null)
            {
                if (labelLower.Contains("name") && !string.IsNullOrEmpty(profile.DisplayName))
                {
                    field.Value = profile.DisplayName;
                }
                else if (labelLower.Contains("language") && !string.IsNullOrEmpty(profile.PreferredLanguage))
                {
                    field.Value = profile.PreferredLanguage;
                }
            }
        }

        return new SparkFormSubmissionPreview
        {
            FormTitle = formTitle,
            TargetUrl = targetUrl,
            Fields = fields
        };
    }

    public Task<string> SubmitApprovedFormAsync(SparkFormSubmissionPreview formPreview, string approvalId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(formPreview);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);

        if (!formPreview.IsValid)
            throw new InvalidOperationException("Cannot submit form with missing required fields.");

        var submissionReceipt = $"SUBMIT_ACK_{Guid.NewGuid():N}";
        return Task.FromResult(submissionReceipt);
    }
}
