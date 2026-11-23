using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Shared.Creative;
using Xunit;

namespace OpenClaw.Shared.Tests.Creative;

public class SparkCreativeEcosystemTests
{
    [Fact]
    public void All16Applications_AreRegisteredAndConfigured()
    {
        var apps = CreativeAppRegistry.AllApplications;
        Assert.Equal(16, apps.Count);

        string[] expectedIds =
        [
            "photospark", "vectorspark", "filmspark", "lightspark",
            "pdfspark", "effectspark", "anispark", "designspark",
            "figspark", "texturespark", "materialspark", "soundspark",
            "wordspark", "deckspark", "cadspark", "gridspark"
        ];

        foreach (var id in expectedIds)
        {
            Assert.True(CreativeAppRegistry.TryGetApplication(id, out var app), $"App '{id}' should be found in registry.");
            Assert.False(string.IsNullOrWhiteSpace(app.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(app.UpstreamName));
            Assert.False(string.IsNullOrWhiteSpace(app.PrimaryCapability));
            Assert.False(string.IsNullOrWhiteSpace(app.MascotMotif));
            Assert.NotEmpty(app.Capabilities);
            Assert.NotEmpty(app.SupportedInterfaces);
            Assert.NotEmpty(app.SupportedFileFormats);
            Assert.Equal(CreativeIntegrationStatus.Tested, app.Status);
        }
    }

    [Fact]
    public void CreativeSkillsEngine_LoadsAll240Skills()
    {
        var registry = CreativeSkillRegistry.Instance;
        Assert.Equal(240, registry.TotalSkillsCount);
        Assert.Equal(240, registry.AllSkills.Count);

        // Verify each skill has all required fields populated
        foreach (var skill in registry.AllSkills)
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.Id));
            Assert.False(string.IsNullOrWhiteSpace(skill.SkillId));
            Assert.False(string.IsNullOrWhiteSpace(skill.Name));
            Assert.False(string.IsNullOrWhiteSpace(skill.Category));
            Assert.False(string.IsNullOrWhiteSpace(skill.CategoryTitle));
            Assert.False(string.IsNullOrWhiteSpace(skill.Application));
            Assert.False(string.IsNullOrWhiteSpace(skill.ApplicationId));
            Assert.False(string.IsNullOrWhiteSpace(skill.Purpose));
            Assert.False(string.IsNullOrWhiteSpace(skill.Path));
        }
    }

    [Fact]
    public void SkillAllocation_MatchesExactSpecifications()
    {
        var registry = CreativeSkillRegistry.Instance;

        var counts = registry.AllSkills
            .GroupBy(s => s.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(25, counts["design_fundamentals"]);
        Assert.Equal(25, counts["ui_ux_interaction"]);
        Assert.Equal(20, counts["raster_image_editing"]);
        Assert.Equal(20, counts["vector_illustration"]);
        Assert.Equal(15, counts["photography_processing"]);
        Assert.Equal(20, counts["video_motion_graphics"]);
        Assert.Equal(15, counts["animation_2d"]);
        Assert.Equal(25, counts["modelling_materials_3d"]);
        Assert.Equal(20, counts["cad_technical_drawing"]);
        Assert.Equal(15, counts["documents_publishing"]);
        Assert.Equal(20, counts["presentations_visualization"]);
        Assert.Equal(20, counts["audio_production_workflows"]);
    }

    [Fact]
    public void CreativeLifecycleManager_PerformsHealthChecks()
    {
        var manager = CreativeLifecycleManager.Instance;
        var health = manager.PerformHealthCheck("vectorspark");

        Assert.Equal("vectorspark", health["applicationId"]);
        Assert.Equal("VectorSpark", health["displayName"]);
        Assert.Equal("Tested", health["status"]);
        Assert.True((bool)health["sourceExists"]);
    }

    [Fact]
    public void CreativeProjectManager_CreatesProjectAndTracksRecent()
    {
        var manager = CreativeProjectManager.Instance;
        var proj = manager.CreateNewProject("photospark", "Unit_Test_Artwork");

        Assert.NotNull(proj);
        Assert.Equal("photospark", proj.ApplicationId);
        Assert.Equal(".pcraft", proj.FileExtension);
        Assert.True(File.Exists(proj.FilePath));

        // Clean up test file
        try { File.Delete(proj.FilePath); } catch { }
    }

    [Fact]
    public async Task CreativeProjectManager_ExecutesCrossAppAssetExchange()
    {
        var manager = CreativeProjectManager.Instance;
        var result = await manager.ExchangeAssetAsync("photospark", "vectorspark", "Test_Input.pcraft");

        Assert.True(result.Success);
        Assert.Equal("PhotoSpark", result.SourceApp);
        Assert.Equal("VectorSpark", result.TargetApp);
        Assert.Equal("RasterToVectorTracing", result.ExchangeType);
        Assert.EndsWith(".svg", result.OutputFilePath);

        try { File.Delete(result.OutputFilePath); } catch { }
    }

    [Fact]
    public void CreativeAgent_ClassifiesIntentsAccurately()
    {
        var agent = CreativeAgent.Instance;

        Assert.Equal("vectorspark", agent.ClassifyIntent("Draw a vector illustration with bezier pen"));
        Assert.Equal("filmspark", agent.ClassifyIntent("Edit this video footage and trim timeline cuts"));
        Assert.Equal("cadspark", agent.ClassifyIntent("Generate orthographic CAD technical drawing with DXF"));
        Assert.Equal("gridspark", agent.ClassifyIntent("Calculate sum formula on spreadsheet grid table"));
        Assert.Equal("soundspark", agent.ClassifyIntent("Apply audio mastering EQ and LUFS normalization"));
        Assert.Equal("figspark", agent.ClassifyIntent("Build responsive UI wireframe component in auto layout"));
    }

    [Fact]
    public async Task CreativeAgent_ExecutesReversibleAction_AutoApproved()
    {
        var agent = CreativeAgent.Instance;
        var res = await agent.ExecutePromptAsync("Create a vector star shape with bezier curves", "vectorspark");

        Assert.Equal(CreativeAgentExecutionState.Completed, res.State);
        Assert.False(res.RequiresApproval);
        Assert.NotEmpty(res.OperationsExecuted);
        Assert.Contains("Execution Complete: VectorSpark", res.ResponseText);
    }

    [Fact]
    public async Task CreativeAgent_RequiresApproval_ForHighImpactDestructiveActions()
    {
        var agent = CreativeAgent.Instance;
        var res = await agent.ExecutePromptAsync("Delete all layers and overwrite the existing project file", "photospark");

        Assert.Equal(CreativeAgentExecutionState.WaitingForApproval, res.State);
        Assert.True(res.RequiresApproval);
        Assert.NotNull(res.ApprovalPrompt);
    }

    [Fact]
    public void CreativeMcpGateway_DiscoversAllTools()
    {
        var gateway = CreativeMcpGateway.Instance;
        var tools = gateway.GetAllTools();

        Assert.True(tools.Count >= 48, "At least 3 tools per 16 applications should be discovered.");
        var photoTools = gateway.GetToolsForApplication("photospark");
        Assert.Contains(photoTools, t => t.Name == "photospark_inspect");
        Assert.Contains(photoTools, t => t.Name == "photospark_modify_element");
    }

    [Fact]
    public void CreativeJsonRpc_ExecutesInspectOperation()
    {
        var req = new CreativeJsonRequest
        {
            Application = "cadspark",
            Operation = "app.inspect"
        };

        var res = CreativeJsonRpc.Execute(req);
        Assert.Equal("success", res.Status);
        Assert.Equal("CADSpark", res.Result["displayName"]?.ToString());
        Assert.Equal("CADCraft", res.Result["upstreamName"]?.ToString());
    }
}
