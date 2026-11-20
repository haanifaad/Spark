using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenClaw.Shared.Tools.Reversal;

/// <summary>
/// Production implementation of the reversal and rollback framework.
/// </summary>
public sealed class SparkReversalManager : ISparkReversalManager
{
    private readonly string _backupDirectory;
    private readonly ConcurrentDictionary<string, SparkReversalMetadata> _records = new();

    public SparkReversalManager(string? customBackupDir = null)
    {
        _backupDirectory = customBackupDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Spark", "ReversalBackups");
        Directory.CreateDirectory(_backupDirectory);
    }

    public async Task<SparkReversalMetadata> CapturePreExecutionStateAsync(string toolName, Dictionary<string, object?> arguments, SparkToolReversibility reversibility, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var opId = Guid.NewGuid().ToString("N");
        string? backupPath = null;
        string? compensationTool = null;
        var compensationArgs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // If operation modifies or deletes an existing file, create an atomic backup copy
        if ((toolName == "fs_write_file" || toolName == "fs_edit_file" || toolName == "fs_delete_file") &&
            arguments.TryGetValue("path", out var pathObj) && pathObj is string filePath && File.Exists(filePath))
        {
            backupPath = Path.Combine(_backupDirectory, $"{opId}_{Path.GetFileName(filePath)}");
            File.Copy(filePath, backupPath, overwrite: true);
            compensationTool = "fs_write_file";
            compensationArgs["path"] = filePath;
            compensationArgs["content"] = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            reversibility = SparkToolReversibility.Reversible;
        }
        else if (toolName == "email_draft")
        {
            compensationTool = "email_draft_discard";
            reversibility = SparkToolReversibility.Reversible;
        }
        else if (toolName == "shopping_checkout" || toolName == "travel_book")
        {
            reversibility = SparkToolReversibility.ReversibleOnlyThroughProviderWorkflow;
        }

        var metadata = new SparkReversalMetadata
        {
            OperationId = opId,
            ToolName = toolName,
            Reversibility = reversibility,
            BackupFilePath = backupPath,
            CompensationToolName = compensationTool,
            CompensationArguments = compensationArgs,
            IdempotencyKey = GenerateIdempotencyKey(toolName, opId),
            CapturedAt = DateTimeOffset.UtcNow
        };

        _records[opId] = metadata;
        return metadata;
    }

    public async Task<bool> AttemptReversalAsync(string operationId, ISparkToolRegistry toolRegistry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(toolRegistry);

        if (!_records.TryGetValue(operationId, out var metadata))
            return false;

        if (metadata.Reversibility == SparkToolReversibility.Irreversible)
            return false;

        // 1. If file backup exists, restore it directly
        if (!string.IsNullOrEmpty(metadata.BackupFilePath) && File.Exists(metadata.BackupFilePath) &&
            metadata.CompensationArguments.TryGetValue("path", out var targetPathObj) && targetPathObj is string targetPath)
        {
            File.Copy(metadata.BackupFilePath, targetPath, overwrite: true);
            return true;
        }

        // 2. If a compensation tool is defined, execute it via the tool registry
        if (!string.IsNullOrEmpty(metadata.CompensationToolName))
        {
            var req = new SparkToolExecutionRequest
            {
                ToolId = metadata.CompensationToolName,
                Arguments = metadata.CompensationArguments,
                CallerIdentity = "SPARK-ReversalManager",
                SessionId = operationId
            };

            var res = await toolRegistry.ExecuteToolAsync(req, ct).ConfigureAwait(false);
            return res.Success;
        }

        return false;
    }

    public SparkReversalMetadata? GetReversalMetadata(string operationId) =>
        _records.TryGetValue(operationId, out var meta) ? meta : null;

    public string GenerateIdempotencyKey(string toolName, string uniqueEntityId)
    {
        var raw = $"{toolName}:{uniqueEntityId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
