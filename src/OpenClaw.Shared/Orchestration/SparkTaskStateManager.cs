using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Thread-safe, atomic-write storage manager for SPARK autonomous tasks and their 14-state lifecycle.
/// </summary>
public sealed class SparkTaskStateManager : ITaskStateManager
{
    private readonly string _baseStorageDir;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SparkTaskStateManager(string? customStorageDir = null)
    {
        _baseStorageDir = customStorageDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Spark", "AutonomousTasks");
        Directory.CreateDirectory(_baseStorageDir);
    }

    private string GetUserDirectory(string userId)
    {
        var clean = Regex.Replace(userId, @"[^a-zA-Z0-9_\-]", "_");
        var dir = Path.Combine(_baseStorageDir, clean);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetTaskFilePath(string userId, string taskId) =>
        Path.Combine(GetUserDirectory(userId), $"{taskId}.json");

    public async Task<SparkTask> CreateTaskAsync(string instruction, string? title = null, SparkTaskPriority priority = SparkTaskPriority.Normal, string userId = "default", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);

        var detectedLang = DetectLanguage(instruction);
        var autoTitle = title ?? GenerateTitle(instruction, detectedLang);

        var task = new SparkTask
        {
            TaskId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Title = autoTitle,
            UserInstruction = instruction.Trim(),
            DetectedLanguage = detectedLang,
            Priority = priority,
            Status = SparkTaskState.Received,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveTaskInternalAsync(task, ct).ConfigureAwait(false);
        return task;
    }

    public async Task<SparkTask?> GetTaskAsync(string taskId, string userId = "default", CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetTaskFilePath(userId, taskId);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SparkTask>(json, JsonOpts);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SparkTask>> GetAllTasksAsync(string userId = "default", CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = GetUserDirectory(userId);
            var files = Directory.GetFiles(dir, "*.json");
            var results = new List<SparkTask>(files.Length);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var item = JsonSerializer.Deserialize<SparkTask>(json, JsonOpts);
                    if (item != null)
                        results.Add(item);
                }
                catch
                {
                    // Ignore corrupted individual files
                }
            }

            return results.OrderByDescending(t => t.CreatedAt).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkTask> UpdateTaskStateAsync(string taskId, SparkTaskState newState, string? statusReason = null, string userId = "default", CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetTaskFilePath(userId, taskId);
            if (!File.Exists(path))
                throw new KeyNotFoundException($"Task '{taskId}' was not found for user '{userId}'.");

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var task = JsonSerializer.Deserialize<SparkTask>(json, JsonOpts)
                       ?? throw new InvalidOperationException($"Corrupt task record for '{taskId}'.");

            ValidateStateTransition(task.Status, newState);

            task.Status = newState;
            task.UpdatedAt = DateTimeOffset.UtcNow;

            if (newState == SparkTaskState.Executing && task.StartedAt == null)
            {
                task.StartedAt = DateTimeOffset.UtcNow;
            }
            else if (newState is SparkTaskState.Completed or SparkTaskState.Failed or SparkTaskState.Cancelled)
            {
                task.CompletedAt = DateTimeOffset.UtcNow;
                if (!string.IsNullOrWhiteSpace(statusReason))
                {
                    if (newState == SparkTaskState.Completed)
                        task.FinalSummary = statusReason;
                    else
                        task.ErrorMessage = statusReason;
                }
            }

            await AtomicWriteAsync(path, task, ct).ConfigureAwait(false);
            return task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkTask> SaveTaskAsync(SparkTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await SaveTaskInternalAsync(task, ct).ConfigureAwait(false);
        return task;
    }

    public async Task<SparkTask> UpdateTaskStepAsync(string taskId, SparkTaskStep step, string userId = "default", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(step);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetTaskFilePath(userId, taskId);
            if (!File.Exists(path))
                throw new KeyNotFoundException($"Task '{taskId}' was not found.");

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var task = JsonSerializer.Deserialize<SparkTask>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Failed to read task.");

            var existingIndex = task.Steps.FindIndex(s => s.StepId == step.StepId || s.Index == step.Index);
            if (existingIndex >= 0)
            {
                task.Steps[existingIndex] = step;
            }
            else
            {
                task.Steps.Add(step);
            }

            task.UpdatedAt = DateTimeOffset.UtcNow;
            await AtomicWriteAsync(path, task, ct).ConfigureAwait(false);
            return task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkTask> ModifyTaskInstructionAsync(string taskId, string newInstruction, string reason = "User edit", string userId = "default", CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newInstruction);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetTaskFilePath(userId, taskId);
            if (!File.Exists(path))
                throw new KeyNotFoundException($"Task '{taskId}' was not found.");

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var task = JsonSerializer.Deserialize<SparkTask>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Failed to read task.");

            task.ModificationHistory.Add(new SparkTaskModificationRecord
            {
                PreviousInstruction = task.UserInstruction,
                NewInstruction = newInstruction.Trim(),
                Reason = reason,
                Timestamp = DateTimeOffset.UtcNow
            });

            task.UserInstruction = newInstruction.Trim();
            task.DetectedLanguage = DetectLanguage(task.UserInstruction);
            task.UpdatedAt = DateTimeOffset.UtcNow;

            // When instructions are modified during active execution or intake, transition to Planning for re-evaluation
            if (task.Status is SparkTaskState.Executing 
                            or SparkTaskState.Ready 
                            or SparkTaskState.WaitingForApproval 
                            or SparkTaskState.Received 
                            or SparkTaskState.Interpreting)
            {
                task.Status = SparkTaskState.Planning;
            }

            await AtomicWriteAsync(path, task, ct).ConfigureAwait(false);
            return task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> RecoverInterruptedTasksAsync(string userId = "default", CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = GetUserDirectory(userId);
            var files = Directory.GetFiles(dir, "*.json");
            var recoveredCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var task = JsonSerializer.Deserialize<SparkTask>(json, JsonOpts);
                    if (task == null) continue;

                    if (task.Status is SparkTaskState.Executing 
                                    or SparkTaskState.Planning 
                                    or SparkTaskState.Interpreting 
                                    or SparkTaskState.Reviewing 
                                    or SparkTaskState.Verifying)
                    {
                        task.Status = SparkTaskState.RequiresUserIntervention;
                        task.ErrorMessage = "Task was interrupted by unexpected application restart or shutdown.";
                        task.UpdatedAt = DateTimeOffset.UtcNow;
                        await AtomicWriteAsync(file, task, ct).ConfigureAwait(false);
                        recoveredCount++;
                    }
                }
                catch
                {
                    // Ignore transient file locks
                }
            }

            return recoveredCount;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteTaskAsync(string taskId, string userId = "default", CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetTaskFilePath(userId, taskId);
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveTaskInternalAsync(SparkTask task, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetTaskFilePath(task.UserId, task.TaskId);
            task.UpdatedAt = DateTimeOffset.UtcNow;
            await AtomicWriteAsync(path, task, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task AtomicWriteAsync(string targetPath, SparkTask task, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        var json = JsonSerializer.Serialize(task, JsonOpts);
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private static void ValidateStateTransition(SparkTaskState from, SparkTaskState to)
    {
        if (from == to)
            return;

        // Terminal states cannot transition to anything
        if (from is SparkTaskState.Completed or SparkTaskState.Failed or SparkTaskState.Cancelled)
        {
            throw new InvalidOperationException($"Cannot transition task from terminal state '{from}' to '{to}'.");
        }

        // Universal transitions: any active state can be Cancelled, Failed, or set to RequiresUserIntervention
        if (to is SparkTaskState.Cancelled or SparkTaskState.Failed or SparkTaskState.RequiresUserIntervention)
            return;

        var isValid = from switch
        {
            SparkTaskState.Received => to is SparkTaskState.Interpreting or SparkTaskState.Planning,
            SparkTaskState.Interpreting => to is SparkTaskState.Planning,
            SparkTaskState.Planning => to is SparkTaskState.WaitingForApproval or SparkTaskState.Ready or SparkTaskState.WaitingForExternalInput,
            SparkTaskState.WaitingForApproval => to is SparkTaskState.Ready or SparkTaskState.Planning or SparkTaskState.Paused,
            SparkTaskState.Ready => to is SparkTaskState.Executing or SparkTaskState.Paused or SparkTaskState.Planning,
            SparkTaskState.Executing => to is SparkTaskState.WaitingForExternalInput or SparkTaskState.Paused or SparkTaskState.Reviewing or SparkTaskState.Verifying or SparkTaskState.Completed,
            SparkTaskState.WaitingForExternalInput => to is SparkTaskState.Executing or SparkTaskState.Planning or SparkTaskState.Paused,
            SparkTaskState.Paused => to is SparkTaskState.Executing or SparkTaskState.Ready or SparkTaskState.Planning,
            SparkTaskState.Reviewing => to is SparkTaskState.Verifying or SparkTaskState.Completed or SparkTaskState.Executing,
            SparkTaskState.Verifying => to is SparkTaskState.Completed or SparkTaskState.Reviewing or SparkTaskState.Executing,
            SparkTaskState.RequiresUserIntervention => to is SparkTaskState.Planning or SparkTaskState.Ready or SparkTaskState.Executing,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid state transition from '{from}' to '{to}'.");
        }
    }

    private static string DetectLanguage(string text)
    {
        var hasMalayalam = Regex.IsMatch(text, @"[\u0D00-\u0D7F]");
        var hasLatin = Regex.IsMatch(text, @"[a-zA-Z]");

        if (hasMalayalam && hasLatin)
            return "Mixed";
        if (hasMalayalam)
            return "Malayalam";
        return "English";
    }

    private static string GenerateTitle(string instruction, string language)
    {
        var trimmed = instruction.Trim().Replace("\r", " ").Replace("\n", " ");
        if (trimmed.Length <= 40)
            return trimmed;

        return trimmed.Substring(0, 37) + "...";
    }
}
