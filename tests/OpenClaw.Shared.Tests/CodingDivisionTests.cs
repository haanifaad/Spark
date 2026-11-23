using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using OpenClaw.Shared;
using OpenClaw.Shared.Commands;
using OpenClaw.Shared.Coding;
using OpenClaw.Shared.Coding.CyberSecurity;
using OpenClaw.Shared.Coding.BuildAnything;
using OpenClaw.Shared.Coding.CliIntelligence;
using OpenClaw.Shared.Coding.ReverseEngineering;

namespace OpenClaw.Shared.Tests;

public class CodingDivisionTests : IDisposable
{
    private readonly string _testWorkspace;

    public CodingDivisionTests()
    {
        _testWorkspace = Path.Combine(Path.GetTempPath(), "SparkTestWorkspace_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWorkspace);
        File.WriteAllText(Path.Combine(_testWorkspace, "index.html"), "<html><body>Spark Test</body></html>");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkspace))
            {
                Directory.Delete(_testWorkspace, true);
            }
        }
        catch
        {
            // Ignore cleanup failure in tests
        }
    }

    [Fact]
    public async Task SlashCommandRegistry_ExecutesCyberSecurityAudit()
    {
        var registry = new SparkSlashCommandRegistry();
        var cmd = registry.FindCommand("cyber-security-audit");
        Assert.NotNull(cmd);
        Assert.Equal(SparkCommandCategory.Security, cmd.Category);

        var context = new SparkCommandExecutionContext
        {
            SessionKey = "test-session",
            WorkspacePath = _testWorkspace,
            IsConnected = true
        };

        var result = await registry.ExecuteLineAsync("/cyber-security-audit target=local-app depth=quick", context);
        Assert.True(result.Succeeded);
        Assert.Equal("StartCyberSecurityAudit", result.ClientAction);
        Assert.NotNull(result.ActionPayload);
    }

    [Fact]
    public async Task SlashCommandRegistry_ExecutesBuildAndCliCommands()
    {
        var registry = new SparkSlashCommandRegistry();
        var context = new SparkCommandExecutionContext
        {
            SessionKey = "test-session",
            WorkspacePath = _testWorkspace,
            IsConnected = true
        };

        var buildResult = await registry.ExecuteLineAsync("/build type=react name=Dashboard", context);
        Assert.True(buildResult.Succeeded);
        Assert.Equal("StartBuildAnything", buildResult.ClientAction);

        var cliResult = await registry.ExecuteLineAsync("/cli harness=blender prompt=\"render cube\"", context);
        Assert.True(cliResult.Succeeded);
        Assert.Equal("OpenCliIntelligence", cliResult.ClientAction);

        var reResult = await registry.ExecuteLineAsync("/reverse-engineer target=test.dll", context);
        Assert.True(reResult.Succeeded);
        Assert.Equal("StartReverseEngineering", reResult.ClientAction);
    }

    [Fact]
    public async Task ShannonAuditEngine_RequiresAuthorization()
    {
        var engine = new ShannonAuditEngine();
        var options = new SecurityAuditOptions
        {
            TargetUrl = "https://example.com/unauthorized",
            IsAuthorizedTarget = false,
            Description = "Unauthorized target test"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await engine.StartAuditAsync(options);
        });
    }

    [Fact]
    public async Task ShannonAuditEngine_RunsFullPipeline_AndGeneratesReports()
    {
        var engine = new ShannonAuditEngine();
        var phasesObserved = new List<AuditPhase>();
        var agentsObserved = new List<SecurityAgentState>();

        engine.PhaseChanged += (s, phase) => phasesObserved.Add(phase);
        engine.AgentStatusChanged += (s, agent) => agentsObserved.Add(agent);

        var options = new SecurityAuditOptions
        {
            TargetUrl = _testWorkspace,
            IsAuthorizedTarget = true,
            Description = "Local test project audit"
        };

        var session = await engine.StartAuditAsync(options);
        Assert.NotNull(session);
        Assert.Equal(AuditPhase.Completed, session.Phase);
        Assert.NotEmpty(phasesObserved);
        Assert.NotEmpty(agentsObserved);
        Assert.NotEmpty(session.Findings);

        // Verify "no exploit, no report" PoC contract
        foreach (var finding in session.Findings)
        {
            Assert.NotEmpty(finding.ExploitationSteps);
            Assert.False(string.IsNullOrWhiteSpace(finding.ProofOfImpact), "Finding must have proof of impact");
            Assert.False(string.IsNullOrWhiteSpace(finding.Remediation), "Finding must have remediation");
        }

        // Test Markdown report generation
        var markdownReport = SecurityReportGenerator.GenerateMarkdownReport(session);
        Assert.Contains("Security Assessment Report", markdownReport);
        Assert.Contains("Executive Summary", markdownReport);
        Assert.Contains("Detailed Exploitation Evidence", markdownReport);

        // Test SARIF 2.1.0 generation
        var sarifReport = SecurityReportGenerator.GenerateSarifReport(session);
        using var jsonDoc = JsonDocument.Parse(sarifReport);
        var root = jsonDoc.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        var runs = root.GetProperty("runs");
        Assert.True(runs.GetArrayLength() > 0);
    }

    [Fact]
    public async Task CraftEngine_ScaffoldsProject_AndDiscoversOutputs()
    {
        var engine = new CraftEngine();
        var targetDir = Path.Combine(_testWorkspace, "SparkDemoWebApp");
        var spec = new CraftProjectSpec
        {
            Name = "SparkDemoWebApp",
            Description = "Create a minimal dashboard",
            TargetType = CraftProjectType.WebSinglePageApp,
            TargetDirectory = targetDir
        };

        var session = await engine.CreateProjectAsync(spec);
        Assert.NotNull(session);
        Assert.NotEmpty(session.GeneratedFiles);
        Assert.NotNull(session.Output);
        Assert.True(session.Output.BuildSuccess);
        Assert.NotNull(session.Output.SiteDirectory);
        Assert.True(Directory.Exists(session.Output.SiteDirectory));
        Assert.NotNull(session.Output.PreviewUrl);
    }

    [Fact]
    public void CliAnythingEngine_EnforcesSafetyTiers()
    {
        // Tier 1 Read Only
        var tierRead = CliAnythingEngine.ClassifySafetyTier("git status");
        Assert.Equal(CliCommandSafetyTier.Tier1_ReadOnly, tierRead);

        var tierList = CliAnythingEngine.ClassifySafetyTier("dir /w");
        Assert.Equal(CliCommandSafetyTier.Tier1_ReadOnly, tierList);

        // Tier 2 Safe Modification
        var tierBuild = CliAnythingEngine.ClassifySafetyTier("dotnet build");
        Assert.Equal(CliCommandSafetyTier.Tier2_SafeModification, tierBuild);

        var tierGitCommit = CliAnythingEngine.ClassifySafetyTier("git commit -m \"fix\"");
        Assert.Equal(CliCommandSafetyTier.Tier2_SafeModification, tierGitCommit);

        // Tier 3 Destructive
        var tierDel = CliAnythingEngine.ClassifySafetyTier("del /f /s /q test");
        Assert.Equal(CliCommandSafetyTier.Tier3_DestructiveOperation, tierDel);

        var tierRm = CliAnythingEngine.ClassifySafetyTier("rm -rf node_modules");
        Assert.Equal(CliCommandSafetyTier.Tier3_DestructiveOperation, tierRm);

        var tierReset = CliAnythingEngine.ClassifySafetyTier("git reset --hard HEAD~1");
        Assert.Equal(CliCommandSafetyTier.Tier3_DestructiveOperation, tierReset);
    }

    [Fact]
    public void CliAnythingEngine_TranslatesNaturalLanguage_AndHasFullCliHubCatalog()
    {
        var engine = new CliAnythingEngine(_testWorkspace);
        Assert.NotEmpty(engine.HarnessRegistry);
        Assert.True(engine.HarnessRegistry.Count >= 84, $"Expected at least 84 harnesses, got {engine.HarnessRegistry.Count}");
        
        // Key harnesses across diverse domains
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "blender");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "gimp");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "libreoffice");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "obsidian");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "freecad");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "audacity");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "godot");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "suno");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "elevenlabs");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "kdenlive");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "comfyui");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "jumpserver");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "openrefine");
        Assert.Contains(engine.HarnessRegistry, h => h.Name == "qgis");

        // Verify capability matrices
        Assert.NotEmpty(engine.Matrices);
        Assert.Equal(5, engine.Matrices.Count);
        Assert.Contains(engine.Matrices, m => m.Id == "video-creation");
        Assert.Contains(engine.Matrices, m => m.Id == "knowledge-research");
        Assert.Contains(engine.Matrices, m => m.Id == "3d-cad");
        Assert.Contains(engine.Matrices, m => m.Id == "game-development");
        Assert.Contains(engine.Matrices, m => m.Id == "image-design");

        // Verify matrix filtering
        var videoTools = engine.FilterByMatrix("video-creation");
        Assert.NotEmpty(videoTools);
        Assert.Contains(videoTools, h => h.Name == "audacity");

        // Verify category filtering
        var audioTools = engine.FilterByCategory("audio");
        Assert.NotEmpty(audioTools);
        Assert.Contains(audioTools, h => h.Name == "suno");

        // Verify search
        var searchResults = engine.SearchHarnesses("music");
        Assert.NotEmpty(searchResults);

        // Natural language translation
        var translatedTest = engine.TranslateNaturalLanguageToCli("run test");
        Assert.Equal("dotnet test", translatedTest);

        var translatedBlender = engine.TranslateNaturalLanguageToCli("export blender scene to obj");
        Assert.Contains("cli-anything-blender", translatedBlender);
    }

    [Fact]
    public async Task ReaEngine_CategorizesTargets_AndSynthesizesCleanRoomSpec()
    {
        var engine = new ReaEngine();

        // Write a mock test file with PE header signature
        var testFile = Path.Combine(_testWorkspace, "MockComponent.dll");
        File.WriteAllBytes(testFile, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 });

        var inspection = await engine.InspectTargetAsync(testFile);
        Assert.NotNull(inspection);
        Assert.Equal(TargetCategory.NetAssembly, inspection.Category);
        Assert.Contains("Clean-Room Architectural Specification", inspection.CleanRoomSpecification);
    }

    [Fact]
    public void CodingDivisionWorkspaceContext_SynchronizesState()
    {
        var context = CodingDivisionWorkspaceContext.Instance;
        context.SetWorkspace(_testWorkspace);

        Assert.Equal(_testWorkspace, context.WorkspaceRoot);

        var auditSession = new SecurityAuditSession
        {
            AuditId = "audit-123",
            TargetUrl = _testWorkspace,
            Options = new SecurityAuditOptions { TargetUrl = _testWorkspace, IsAuthorizedTarget = true }
        };
        context.ActiveAuditSession = auditSession;
        Assert.Equal("audit-123", context.ActiveAuditSession?.AuditId);

        var craftSession = new CraftCreationSession
        {
            SessionId = "craft-123",
            Spec = new CraftProjectSpec
            {
                Name = "CraftDemo",
                Description = "Demo App",
                TargetDirectory = _testWorkspace
            }
        };
        context.ActiveBuildSession = craftSession;
        Assert.Equal("craft-123", context.ActiveBuildSession?.SessionId);

        var execRecord = new CliExecutionRecord
        {
            Id = "cli-1",
            Command = "git status",
            WorkingDirectory = _testWorkspace,
            ExitCode = 0,
            ExecutedAt = DateTimeOffset.UtcNow
        };
        context.CliHistory.Add(execRecord);
        Assert.Contains(context.CliHistory, r => r.Command == "git status");
    }

    [Fact]
    public async Task CliAnythingEngine_ExecutesCommand_AndRecordsHistory()
    {
        var engine = new CliAnythingEngine(_testWorkspace);
        var record = await engine.ExecuteAsync("cmd.exe /c echo Hello SPARK CLI Engine");

        Assert.NotNull(record);
        Assert.Equal(0, record.ExitCode);
        Assert.Contains("Hello SPARK CLI Engine", record.StandardOutput);
        Assert.Contains(engine.ExecutionHistory, r => r.Id == record.Id);
    }

    [Fact]
    public async Task SparkSlashCommandRegistry_ExecutesCliListAndHarnessQueries()
    {
        var registry = new SparkSlashCommandRegistry();
        var context = new SparkCommandExecutionContext
        {
            SessionKey = "test-session",
            WorkspacePath = _testWorkspace,
            IsConnected = true
        };

        // Test /cli list
        var listResult = await registry.ExecuteLineAsync("/cli list", context);
        Assert.True(listResult.Succeeded);
        Assert.Contains("SPARK Built-In CLI Hub Catalog", listResult.OutputMessage);
        Assert.Contains("video-creation", listResult.OutputMessage);

        // Test /cli obsidian
        var toolResult = await registry.ExecuteLineAsync("/cli obsidian", context);
        Assert.True(toolResult.Succeeded);
        Assert.Contains("Obsidian", toolResult.OutputMessage);
        Assert.Equal("cli-anything-obsidian --help", toolResult.ActionPayload);
    }
}
