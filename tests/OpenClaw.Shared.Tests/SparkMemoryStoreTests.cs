using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Memory;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkMemoryStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SparkMemoryStore _store;

    public SparkMemoryStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SparkMemoryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new SparkMemoryStore(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task RememberAsync_StoresAndRetrievesMemoryById()
    {
        var entry = new MemoryRecord
        {
            Id = "mem-1",
            Content = "User prefers dark theme and C# with .NET 10",
            Scope = MemoryScope.UserProfile,
            Category = MemoryCategory.Preference,
            Tags = new[] { "theme", "dotnet" }
        };

        var stored = await _store.RememberAsync(entry);
        Assert.Equal("mem-1", stored.Id);
        Assert.Equal(1, stored.Version);

        var retrieved = await _store.GetAsync("mem-1");
        Assert.NotNull(retrieved);
        Assert.Equal(entry.Content, retrieved.Content);
        Assert.Equal(MemoryScope.UserProfile, retrieved.Scope);
        Assert.Equal(MemoryCategory.Preference, retrieved.Category);
    }

    [Fact]
    public async Task UpdateAsync_IncrementsVersionAndPreservesHistory()
    {
        var entry = new MemoryRecord
        {
            Id = "mem-versioned",
            Content = "Target framework is .NET 9",
            Scope = MemoryScope.Project,
            Category = MemoryCategory.ProjectConvention
        };

        await _store.RememberAsync(entry);

        var updated = await _store.UpdateAsync("mem-versioned", "Target framework updated to .NET 10", "Migrated to .NET 10");
        Assert.Equal(2, updated.Version);
        Assert.Equal("Target framework updated to .NET 10", updated.Content);

        var history = await _store.GetHistoryAsync("mem-versioned");
        Assert.Single(history);
        Assert.Equal(1, history[0].Version);
        Assert.Equal("Target framework is .NET 9", history[0].Content);
        Assert.Equal("Migrated to .NET 10", history[0].Reason);
    }

    [Fact]
    public async Task ForgetAsync_RemovesMemorySuccessfully()
    {
        var entry = new MemoryRecord
        {
            Id = "mem-forget",
            Content = "Temporary reminder to review pull request",
            Scope = MemoryScope.WorkingMemory
        };

        await _store.RememberAsync(entry);
        Assert.NotNull(await _store.GetAsync("mem-forget"));

        var forgotten = await _store.ForgetAsync("mem-forget");
        Assert.True(forgotten);

        var check = await _store.GetAsync("mem-forget");
        Assert.Null(check);
    }

    [Fact]
    public async Task SearchAsync_RanksByRelevanceAndBoostsPinned()
    {
        await _store.RememberAsync(new MemoryRecord
        {
            Id = "mem-a",
            Content = "OpenClaw Gateway uses WebSocket protocol with JSON-RPC",
            Scope = MemoryScope.Semantic,
            Tags = new[] { "gateway", "protocol" }
        });

        await _store.RememberAsync(new MemoryRecord
        {
            Id = "mem-b",
            Content = "The gateway client supports automatic reconnection and pairing",
            Scope = MemoryScope.Semantic,
            IsPinned = true,
            Tags = new[] { "gateway", "pairing" }
        });

        await _store.RememberAsync(new MemoryRecord
        {
            Id = "mem-c",
            Content = "Audio synthesis uses Kokoro and Piper neural voice models",
            Scope = MemoryScope.Semantic,
            Tags = new[] { "audio", "voice" }
        });

        var results = await _store.SearchAsync("gateway pairing");
        Assert.NotEmpty(results);
        Assert.Equal("mem-b", results[0].Record.Id); // Pinned and matched both terms
        Assert.True(results[0].RelevanceScore > 0);
    }

    [Fact]
    public async Task UserIsolation_EnforcesStrictBoundariesBetweenUsers()
    {
        var recordUser1 = new MemoryRecord
        {
            Id = "secret-1",
            UserId = "user_alpha",
            Content = "Alpha's private working notes"
        };

        var recordUser2 = new MemoryRecord
        {
            Id = "secret-2",
            UserId = "user_beta",
            Content = "Beta's private working notes"
        };

        await _store.RememberAsync(recordUser1);
        await _store.RememberAsync(recordUser2);

        // Alpha cannot retrieve Beta's memory
        var retrievedByAlpha = await _store.GetAsync("secret-2", userId: "user_alpha");
        Assert.Null(retrievedByAlpha);

        // Beta cannot retrieve Alpha's memory
        var retrievedByBeta = await _store.GetAsync("secret-1", userId: "user_beta");
        Assert.Null(retrievedByBeta);

        // Each user retrieves their own
        Assert.NotNull(await _store.GetAsync("secret-1", userId: "user_alpha"));
        Assert.NotNull(await _store.GetAsync("secret-2", userId: "user_beta"));
    }

    [Fact]
    public async Task ConflictResolution_SupersedesContradictoryFactWithinCategory()
    {
        var mem1 = new MemoryRecord
        {
            Id = "rule-1",
            Content = "Use tabs for indentation across all C# projects",
            Scope = MemoryScope.Project,
            Category = MemoryCategory.ProjectConvention,
            ProjectId = "spark-core"
        };
        await _store.RememberAsync(mem1);

        // New observation with high similarity in the same category and project
        var mem2 = new MemoryRecord
        {
            Id = "rule-2",
            Content = "Use 4 spaces for indentation across all C# projects",
            Scope = MemoryScope.Project,
            Category = MemoryCategory.ProjectConvention,
            ProjectId = "spark-core",
            Provenance = "Code review guideline update"
        };
        var stored = await _store.RememberAsync(mem2);

        // Should update rule-1 rather than creating duplicate
        Assert.Equal("rule-1", stored.Id);
        Assert.Equal(2, stored.Version);
        Assert.Contains("4 spaces", stored.Content);
        Assert.Single(stored.Revisions);
        Assert.Contains("tabs", stored.Revisions[0].Content);
    }

    [Fact]
    public async Task UserProfileAndProjectMemory_PersistCorrectly()
    {
        var profile = new UserProfile
        {
            UserId = "dev_1",
            DisplayName = "Aadith",
            PreferredLanguage = "ml",
            RoleOrOccupation = "Software Architect",
            WorkingConventions = new[] { "Prefer test-driven development", "Concise summaries" }
        };

        await _store.SaveUserProfileAsync(profile);
        var loadedProfile = await _store.GetUserProfileAsync("dev_1");
        Assert.Equal("Aadith", loadedProfile.DisplayName);
        Assert.Equal("ml", loadedProfile.PreferredLanguage);
        Assert.Equal(2, loadedProfile.WorkingConventions.Count);

        var project = new ProjectMemory
        {
            ProjectId = "spark",
            Name = "SPARK Autonomous Agent",
            TechStack = new[] { "C#", ".NET 10", "WinUI 3", "Node.js" },
            ArchitecturalRules = new[] { "Strict repository boundary", "Human-in-the-loop approvals" }
        };

        await _store.SaveProjectMemoryAsync(project, userId: "dev_1");
        var loadedProject = await _store.GetProjectMemoryAsync("spark", userId: "dev_1");
        Assert.NotNull(loadedProject);
        Assert.Equal("SPARK Autonomous Agent", loadedProject.Name);
        Assert.Equal(4, loadedProject.TechStack.Count);
    }

    [Fact]
    public async Task BackupAndRestore_ExportsAndImportsCompleteDataset()
    {
        await _store.RememberAsync(new MemoryRecord
        {
            Id = "rec-1",
            Content = "User key insight",
            UserId = "backup_user"
        });

        await _store.SaveUserProfileAsync(new UserProfile
        {
            UserId = "backup_user",
            DisplayName = "Backup Tester"
        });

        var backupJson = await _store.ExportBackupJsonAsync("backup_user");
        Assert.Contains("User key insight", backupJson);
        Assert.Contains("Backup Tester", backupJson);

        // Restore into another user partition
        var restoredCount = await _store.RestoreBackupJsonAsync(backupJson, overwrite: true, userId: "restored_user");
        Assert.Equal(1, restoredCount);

        var restoredRecord = await _store.GetAsync("rec-1", userId: "restored_user");
        Assert.NotNull(restoredRecord);
        Assert.Equal("User key insight", restoredRecord.Content);

        var restoredProfile = await _store.GetUserProfileAsync("restored_user");
        Assert.Equal("Backup Tester", restoredProfile.DisplayName);
    }
}
