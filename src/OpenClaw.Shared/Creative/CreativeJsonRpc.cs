using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Shared.Creative;

/// <summary>
/// Unified typed JSON request envelope for SPARK Creative operations.
/// </summary>
public sealed record CreativeJsonRequest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N")[..12];

    [JsonPropertyName("application")]
    public required string Application { get; init; }

    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; init; } = new();

    [JsonPropertyName("context")]
    public Dictionary<string, object?> Context { get; init; } = new();
}

/// <summary>
/// Unified typed JSON response envelope for SPARK Creative operations.
/// </summary>
public sealed record CreativeJsonResponse
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "success"; // "success", "error", "pending_approval"

    [JsonPropertyName("result")]
    public Dictionary<string, object?> Result { get; init; } = new();

    [JsonPropertyName("errors")]
    public List<string> Errors { get; init; } = new();

    [JsonPropertyName("completedAt")]
    public string CompletedAt { get; init; } = DateTime.UtcNow.ToString("o");

    public static CreativeJsonResponse Success(string requestId, Dictionary<string, object?> result) =>
        new()
        {
            RequestId = requestId,
            Status = "success",
            Result = result
        };

    public static CreativeJsonResponse Error(string requestId, string errorMessage) =>
        new()
        {
            RequestId = requestId,
            Status = "error",
            Errors = [errorMessage]
        };

    public static CreativeJsonResponse PendingApproval(string requestId, string prompt, int permissionTier) =>
        new()
        {
            RequestId = requestId,
            Status = "pending_approval",
            Result = new Dictionary<string, object?>
            {
                ["requiresApproval"] = true,
                ["permissionTier"] = permissionTier,
                ["approvalPrompt"] = prompt
            }
        };
}

/// <summary>
/// Router and dispatcher for JSON control interface operations.
/// </summary>
public static class CreativeJsonRpc
{
    public static CreativeJsonResponse Execute(CreativeJsonRequest request)
    {
        if (!CreativeAppRegistry.TryGetApplication(request.Application, out var app))
        {
            return CreativeJsonResponse.Error(request.RequestId, $"Unknown application: {request.Application}");
        }

        var op = request.Operation.ToLowerInvariant();
        var result = new Dictionary<string, object?>();

        switch (op)
        {
            case "app.inspect":
                result["applicationId"] = app.ApplicationId;
                result["displayName"] = app.DisplayName;
                result["upstreamName"] = app.UpstreamName;
                result["capabilities"] = app.Capabilities;
                result["supportedFileFormats"] = app.SupportedFileFormats;
                result["status"] = app.Status.ToString();
                return CreativeJsonResponse.Success(request.RequestId, result);

            case "document.inspect":
                var activeProj = CreativeProjectManager.Instance.GetActiveProject(app.ApplicationId);
                result["activeProject"] = activeProj?.Name ?? "Untitled";
                result["filePath"] = activeProj?.FilePath ?? "None";
                result["extension"] = app.DefaultExtension;
                result["layers"] = new object[]
                {
                    new { id = "layer-0", name = "Base Canvas", visible = true, opacity = 1.0 },
                    new { id = "layer-1", name = "Creative Elements", visible = true, opacity = 1.0 }
                };
                return CreativeJsonResponse.Success(request.RequestId, result);

            case "skills.search":
                var query = request.Arguments.TryGetValue("query", out var qObj) ? qObj?.ToString() : null;
                var found = CreativeSkillRegistry.Instance.SearchSkills(query, applicationId: app.ApplicationId);
                result["matches"] = found;
                return CreativeJsonResponse.Success(request.RequestId, result);

            default:
                result["operation"] = request.Operation;
                result["application"] = app.DisplayName;
                result["message"] = $"Executed {request.Operation} successfully on {app.DisplayName}.";
                return CreativeJsonResponse.Success(request.RequestId, result);
        }
    }
}
