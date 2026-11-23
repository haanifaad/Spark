using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using OpenClaw.Chat;
using OpenClaw.Shared.Browser;
using OpenClaw.Shared.Capabilities;
using OpenClaw.Shared.Memory;
using OpenClaw.Shared.Routines;
using OpenClaw.Shared.Security;
using OpenClaw.Shared.Specialists;
using OpenClaw.Shared.Watchdog;

namespace OpenClaw.Shared.Tests;

public class SparkAutonomousCapabilityTests
{
    // ==========================================
    // 1. Windows Safety Floor Tests
    // ==========================================
    [Theory]
    [InlineData("Remove-Item -Recurse -Force C:\\")]
    [InlineData("rm -rf C:\\Windows")]
    [InlineData("del /s /q C:\\")]
    [InlineData("rd /s /q C:\\Users")]
    [InlineData("rmdir /s /q C:\\")]
    public void WindowsSafetyFloor_DestructiveRootDeletions_AreBlocked(string cmd)
    {
        var verdict = WindowsSafetyFloor.Evaluate(cmd);
        Assert.False(verdict.IsAllowed);
        Assert.Equal("delete-root-system", verdict.BlockedRule);
    }

    [Theory]
    [InlineData("Format-Volume -DriveLetter C")]
    [InlineData("format C: /FS:NTFS /Q")]
    [InlineData("diskpart /s script.txt")]
    [InlineData("Clear-Disk -Number 0")]
    public void WindowsSafetyFloor_DiskFormatting_IsBlocked(string cmd)
    {
        var verdict = WindowsSafetyFloor.Evaluate(cmd);
        Assert.False(verdict.IsAllowed);
        Assert.Equal("format-disk", verdict.BlockedRule);
    }

    [Fact]
    public void WindowsSafetyFloor_GitForcePush_IsBlocked()
    {
        var verdict = WindowsSafetyFloor.Evaluate("git push origin main --force");
        Assert.False(verdict.IsAllowed);
        Assert.Equal("force-push", verdict.BlockedRule);

        verdict = WindowsSafetyFloor.Evaluate("git push -f");
        Assert.False(verdict.IsAllowed);
    }

    [Fact]
    public void WindowsSafetyFloor_ForkBomb_IsBlocked()
    {
        var verdict = WindowsSafetyFloor.Evaluate(":(){ :|:& };:");
        Assert.False(verdict.IsAllowed);
        Assert.Equal("fork-bomb", verdict.BlockedRule);
    }

    [Fact]
    public void WindowsSafetyFloor_RegistryRootDelete_IsBlocked()
    {
        var verdict = WindowsSafetyFloor.Evaluate("reg delete HKLM\\Software\\Test /f");
        Assert.False(verdict.IsAllowed);
        Assert.Equal("registry-system-delete", verdict.BlockedRule);
    }

    [Fact]
    public void WindowsSafetyFloor_EncodedDestructiveCommand_IsDecodedAndBlocked()
    {
        // "Remove-Item -Recurse -Force C:\" in UTF-16LE Base64
        var script = "Remove-Item -Recurse -Force C:\\";
        var base64 = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var command = $"powershell.exe -NoProfile -enc {base64}";

        var verdict = WindowsSafetyFloor.Evaluate(command);
        Assert.False(verdict.IsAllowed);
        Assert.Contains("encoded:", verdict.BlockedRule);
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("dotnet build -c Release")]
    [InlineData("Get-ChildItem -Path .")]
    [InlineData("echo 'Hello World'")]
    [InlineData("npm test")]
    public void WindowsSafetyFloor_SafeCommands_AreAllowed(string cmd)
    {
        var verdict = WindowsSafetyFloor.Evaluate(cmd);
        Assert.True(verdict.IsAllowed);
    }

    // ==========================================
    // 2. HMAC Approval Guard Tests
    // ==========================================
    [Fact]
    public void HmacApprovalGuard_ValidTicket_Passes()
    {
        using var guard = new HmacApprovalGuard();
        var ticket = guard.CreateTicket("system.run", "{\"command\":\"dir\"}");
        var isValid = guard.ValidateTicket(ticket, "system.run", "{\"command\":\"dir\"}");
        Assert.True(isValid);
    }

    [Fact]
    public void HmacApprovalGuard_ReplayedTicket_IsRejected()
    {
        using var guard = new HmacApprovalGuard();
        var ticket = guard.CreateTicket("system.run", "{\"command\":\"dir\"}");

        // First validation consumes the nonce
        var firstValid = guard.ValidateTicket(ticket, "system.run", "{\"command\":\"dir\"}");
        Assert.True(firstValid);

        // Immediate replay of the exact same ticket must fail!
        var secondValid = guard.ValidateTicket(ticket, "system.run", "{\"command\":\"dir\"}");
        Assert.False(secondValid);
    }

    [Fact]
    public void HmacApprovalGuard_TamperedArguments_IsRejected()
    {
        using var guard = new HmacApprovalGuard();
        var ticket = guard.CreateTicket("system.run", "{\"command\":\"echo 1\"}");
        var isValid = guard.ValidateTicket(ticket, "system.run", "{\"command\":\"echo 2\"}");
        Assert.False(isValid);
    }

    // ==========================================
    // 3. Turn Watchdog Monitor Tests
    // ==========================================
    [Fact]
    public void TurnWatchdog_DetectsSilentStall_AndRedactsSecrets()
    {
        var watchdog = new TurnWatchdogMonitor();
        var turn = new TurnWatchdogMonitor.WatchedTurn
        {
            TurnId = "turn_123",
            SessionId = "sess_abc",
            ToolName = "system.run",
            Command = "curl -H 'Authorization: Bearer sk-secretkey1234567890abcdef' https://api.com",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastHeartbeatAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        watchdog.RegisterTurn(turn);
        var stalls = watchdog.CheckStalls(TimeSpan.FromMinutes(2));

        Assert.Single(stalls);
        Assert.Equal("stalled-command", stalls[0].Reason);
        Assert.DoesNotContain("sk-secretkey", stalls[0].NoticeMessage);
        Assert.Contains("[REDACTED_SECRET]", stalls[0].NoticeMessage);
    }

    // ==========================================
    // 4. Deterministic Context Compactor Tests
    // ==========================================
    [Fact]
    public void DeterministicContextCompactor_CompactsOldExchanges_PreservingLastN()
    {
        var items = new List<ChatTimelineItem>();
        for (var i = 0; i < 10; i++)
        {
            items.Add(new ChatTimelineItem($"u_{i}", ChatTimelineItemKind.User, $"User message {i} with a lot of long text to artificially raise token count {new string('x', 3000)}"));
            items.Add(new ChatTimelineItem($"a_{i}", ChatTimelineItemKind.Assistant, $"Assistant response {i} explaining complex solutions {new string('y', 3000)}"));
        }

        var compacted = DeterministicContextCompactor.CompactIfNeeded(items, keepExchanges: 3, tokenBudget: 4000);

        // Result should start with summary item, then recent 3 user exchanges (3 user + 3 assistant = 6 items)
        Assert.True(compacted.Count < items.Count);
        Assert.Contains("Summary of earlier messages", compacted[0].Text);
        Assert.Equal("User message 7", compacted[1].Text[..14]);
    }

    // ==========================================
    // 5. Autonomous Routine Scheduler Tests
    // ==========================================
    [Fact]
    public void AutonomousRoutineScheduler_StopsOnConsecutiveErrors_AndIterationCeiling()
    {
        var scheduler = new AutonomousRoutineScheduler();
        var routine = new AutonomousRoutine
        {
            Id = "daily_briefing",
            Title = "Morning Briefing",
            Prompt = "Summarize latest emails and schedule.",
            Interval = TimeSpan.FromHours(24),
            MaxConsecutiveErrors = 2,
            MaxIterations = 5
        };

        scheduler.RegisterRoutine(routine);

        // Simulate 2 consecutive failures
        scheduler.RecordExecutionOutcome(routine.Id, false, "Network timeout 1");
        Assert.True(routine.IsEnabled);

        scheduler.RecordExecutionOutcome(routine.Id, false, "Network timeout 2");
        Assert.False(routine.IsEnabled);
        Assert.Equal(RoutineStatus.PausedErrors, routine.Status);
    }

    // ==========================================
    // 6. Browser SSRF Guard Tests
    // ==========================================
    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("http://127.0.0.1:18789/gateway")]
    [InlineData("http://192.168.1.1/admin")]
    [InlineData("http://10.0.0.5:3000")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public void BrowserSsrfGuard_PrivateAndLoopback_AreBlocked(string url)
    {
        var (allowed, reason) = BrowserSsrfGuard.CheckUrl(url);
        Assert.False(allowed);
        Assert.NotNull(reason);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://www.google.com/search?q=spark")]
    [InlineData("https://github.com/haanifaad/Spark")]
    public void BrowserSsrfGuard_PublicUrls_AreAllowed(string url)
    {
        var (allowed, reason) = BrowserSsrfGuard.CheckUrl(url);
        Assert.True(allowed);
        Assert.Null(reason);
    }

    // ==========================================
    // 7. Team Room Service Tests
    // ==========================================
    [Fact]
    public async Task TeamRoomService_ParsesMentions_AndEnforcesDepthCeiling()
    {
        var service = new TeamRoomService();
        var room = service.CreateRoom("Sprint Planning", "Feature delivery", ["email_agent", "coder_agent"]);

        var msg = await service.PostMessageAsync(room.RoomId, "user", "Please check this bug @coder_agent and notify @email_agent");
        Assert.Contains("coder_agent", msg.Mentions);
        Assert.Contains("email_agent", msg.Mentions);

        // Verify recursion depth limit
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PostMessageAsync(room.RoomId, "coder_agent", "Loop", currentDepth: 6));
    }

    // ==========================================
    // 8. Structured Memory Journal Tests
    // ==========================================
    [Fact]
    public async Task StructuredMemoryJournal_RecordAndRollback_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"spark_jrn_test_{Guid.NewGuid():N}");
        try
        {
            var journal = new StructuredMemoryJournal(tempDir);
            var initial = "# User Preferences\nTheme: Dark\n";
            var edited = "# User Preferences\nTheme: Light\nFont: Segoe\n";

            var entry = await journal.RecordChangeAsync("MEMORY.md", initial, edited, "user");
            Assert.NotNull(entry);
            Assert.Equal("edited", entry.Kind);

            // Rollback to initial
            var restored = await journal.RollbackAsync(entry.Id, edited);
            Assert.Equal(initial, restored);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task StructuredMemoryJournal_SingleParameterRollback_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"spark_jrn_test_{Guid.NewGuid():N}");
        try
        {
            var journal = new StructuredMemoryJournal(tempDir);
            var initial = "Rule: Always ask before committing";
            var edited = "Rule: Commit without asking";

            var entry = await journal.RecordChangeAsync("RULES.md", initial, edited, "assistant");
            Assert.NotNull(entry);

            // 1-Click Rollback without providing currentContent
            var restored = await journal.RollbackAsync(entry.Id);
            Assert.Equal(initial, restored);

            // History should contain both original change and rollback action
            var history = await journal.GetHistoryAsync();
            Assert.Equal(2, history.Count);
            Assert.Equal("restored", history[0].Kind);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ==========================================
    // 9. Memory Heuristic Proposer Tests
    // ==========================================
    [Fact]
    public async Task MemoryHeuristicProposer_ExtractsEnduringFacts()
    {
        var proposer = new MemoryHeuristicProposer();
        var proposals = proposer.ScanUserMessage("Please remember that I prefer dark mode and clean code.");

        Assert.Single(proposals);
        Assert.Contains("I prefer dark mode and clean code", proposals[0].ProposedFact);

        // Questions should not trigger proposals
        var questions = proposer.ScanUserMessage("Do you remember where I live?");
        Assert.Empty(questions);

        // Accept proposal
        var accepted = await proposer.AcceptProposalAsync(proposals[0].Id);
        Assert.True(accepted);
    }

    // ==========================================
    // 10. Specialist Coordinator Tests
    // ==========================================
    [Fact]
    public async Task SpecialistCoordinator_ResolvesAndDispatchesToSpecialists()
    {
        var coordinator = new SpecialistCoordinator();

        // 1. Verify default registrations
        var all = coordinator.GetAllSpecialists();
        Assert.True(all.Count >= 6);

        // 2. Resolve known tags
        var travel = coordinator.ResolveSpecialist("@travel");
        Assert.NotNull(travel);
        Assert.Equal("travel_specialist", travel.Id);

        var coder = coordinator.ResolveSpecialist("coder");
        Assert.NotNull(coder);
        Assert.Equal("coder_specialist", coder.Id);

        // 3. Dispatch to @travel specialist
        var travelResult = await coordinator.DispatchAsync("@travel", "Check Vande Bharat trains from New Delhi to Varanasi");
        Assert.True(travelResult.Success);
        Assert.Equal("travel_specialist", travelResult.SpecialistId);
        Assert.Contains("Travel", travelResult.ResponseText);

        // 4. Enforce recursion depth ceiling
        var blockedResult = await coordinator.DispatchAsync("@coder", "Refactor loop", currentDepth: SpecialistCoordinator.MaxChainDepth + 1);
        Assert.False(blockedResult.Success);
        Assert.Equal("system_guard", blockedResult.SpecialistId);
        Assert.Contains("maximum recursion depth", blockedResult.ResponseText);

        // 5. Unknown tag handling
        var unknownResult = await coordinator.DispatchAsync("@nonexistent", "Do something");
        Assert.False(unknownResult.Success);
        Assert.Contains("No specialist registered", unknownResult.ResponseText);
    }

    // ==========================================
    // 11. Topic Memory Store Tests
    // ==========================================
    [Fact]
    public async Task TopicMemoryStore_ReadWriteAppendAndRollback_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"spark_topic_test_{Guid.NewGuid():N}");
        try
        {
            var store = new TopicMemoryStore(tempDir);

            // 1. Write root topic (MEMORY.md)
            var rootDoc = await store.WriteTopicAsync("root", "# User Preferences\nLanguage: C#\n");
            Assert.Equal("MEMORY.md", rootDoc.RelativePath);
            Assert.Contains("Language: C#", rootDoc.Content);

            // 2. Read root topic
            var readDoc = await store.ReadTopicAsync("root");
            Assert.NotNull(readDoc);
            Assert.Equal(rootDoc.Sha256Hash, readDoc.Sha256Hash);

            // 3. Create topic and then append to it
            await store.WriteTopicAsync("coding", "# Coding Guidelines\n", actor: "user");
            var appended = await store.AppendSectionAsync("coding", "Conventions", "Always write unit tests.");
            Assert.Equal("memory/coding.md", appended.RelativePath);
            Assert.Contains("Always write unit tests.", appended.Content);

            // 4. Optimistic concurrency conflict
            await Assert.ThrowsAsync<MemoryConflictException>(() =>
                store.WriteTopicAsync("coding", "Overwritten content", expectedBeforeHash: "invalid_hash_value"));

            // 5. Journal integration and 1-click rollback
            var history = await store.Journal.GetHistoryAsync();
            Assert.NotEmpty(history);
            var appendEntry = history.FirstOrDefault(h => h.Path == "memory/coding.md" && h.Kind == "edited");
            Assert.NotNull(appendEntry);

            // Roll back the append to restore initial guidelines
            var rolledBackContent = await store.Journal.RollbackAsync(appendEntry.Id);
            Assert.NotNull(rolledBackContent);
            Assert.Equal("# Coding Guidelines\n", rolledBackContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ==========================================
    // 12. Isolated Browser Capability Tests
    // ==========================================
    [Fact]
    public async Task IsolatedBrowserCapability_LiveEdgeOrSimulated_Succeeds()
    {
        var tempProfileDir = Path.Combine(Path.GetTempPath(), $"spark_browser_test_{Guid.NewGuid():N}");
        try
        {
            var browser = new IsolatedBrowserCapability(new NullLogger(), tempProfileDir);

            // 1. SSRF prevention
            var ssrfRequest = new NodeInvokeRequest
            {
                Id = "req_ssrf",
                Command = IsolatedBrowserCapability.NavigateCommand,
                Args = JsonDocument.Parse("{\"url\":\"http://127.0.0.1:18789/gateway\"}").RootElement
            };
            var ssrfResponse = await browser.ExecuteAsync(ssrfRequest);
            Assert.False(ssrfResponse.Ok);
            Assert.Equal("SSRF_EGRESS_BLOCKED", ssrfResponse.ErrorCode);

            // 2. Safe navigation
            var navRequest = new NodeInvokeRequest
            {
                Id = "req_nav",
                Command = IsolatedBrowserCapability.NavigateCommand,
                Args = JsonDocument.Parse("{\"url\":\"https://example.com\"}").RootElement
            };
            var navResponse = await browser.ExecuteAsync(navRequest);
            Assert.True(navResponse.Ok);

            // 3. Browser click
            var clickRequest = new NodeInvokeRequest
            {
                Id = "req_click",
                Command = IsolatedBrowserCapability.ClickCommand,
                Args = JsonDocument.Parse("{\"selector\":\"#submit-btn\"}").RootElement
            };
            var clickResponse = await browser.ExecuteAsync(clickRequest);
            Assert.True(clickResponse.Ok);

            // 4. Browser type
            var typeRequest = new NodeInvokeRequest
            {
                Id = "req_type",
                Command = IsolatedBrowserCapability.TypeCommand,
                Args = JsonDocument.Parse("{\"selector\":\"#search-input\",\"text\":\"SPARK AI\"}").RootElement
            };
            var typeResponse = await browser.ExecuteAsync(typeRequest);
            Assert.True(typeResponse.Ok);
        }
        finally
        {
            if (Directory.Exists(tempProfileDir))
            {
                try
                {
                    Directory.Delete(tempProfileDir, recursive: true);
                }
                catch
                {
                    // Ignore profile directory locked cleanup in temp
                }
            }
        }
    }
}
