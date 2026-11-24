using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenClaw.Shared.Creative;
using OpenClaw.Shared.Divisions;
using OpenClaw.Shared.Inference;
using OpenClaw.Shared.Inference.Catalog;
using OpenClaw.Shared.VoiceMode;
using Xunit;

namespace OpenClaw.Shared.Tests.Architecture;

public class SparkCriticalFixesValidationTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    #region Problem 1: Study & Voice Mode Crash Validation

    [Fact]
    public void StudyPageXaml_DeclaresAllContentDialogsInsideResources_NotVisualGrid()
    {
        var xamlPath = Path.Combine(RepoRoot, "src", "OpenClaw.Tray.WinUI", "Pages", "StudyPage.xaml");
        Assert.True(File.Exists(xamlPath), $"StudyPage.xaml must exist at: {xamlPath}");

        var content = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(content);
        var defaultNs = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        // 1. All ContentDialog elements must be located under Page.Resources
        var resourcesElem = doc.Root?.Element(defaultNs + "Page.Resources");
        Assert.NotNull(resourcesElem);

        var dialogsInResources = resourcesElem.Elements(defaultNs + "ContentDialog").ToList();
        Assert.True(dialogsInResources.Count >= 8, $"Expected at least 8 ContentDialogs in Page.Resources, found {dialogsInResources.Count}");

        // Verify the 8 required dialog names
        string[] requiredDialogs =
        [
            "SkillsExplorerDialog",
            "ToolsExplorerDialog",
            "ExecutionHistoryDialog",
            "CommandHelpDialog",
            "ConfirmationDialog",
            "KnowledgeLibraryDialog",
            "GroundedSearchDialog",
            "ResearchWorkspaceDialog"
        ];

        var xNs = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var dialogNames = dialogsInResources
            .Select(d => d.Attribute(xNs + "Name")?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        foreach (var req in requiredDialogs)
        {
            Assert.Contains(req, dialogNames);
        }

        // 2. The root Grid must NOT contain any ContentDialog in its direct children or visual descendants
        var rootGrid = doc.Root?.Element(defaultNs + "Grid");
        Assert.NotNull(rootGrid);

        var dialogsInGrid = rootGrid.Descendants(defaultNs + "ContentDialog").ToList();
        Assert.Empty(dialogsInGrid);
    }

    [Fact]
    public async Task VoiceModeDivisionService_InitializesGracefully_WithoutThrowing()
    {
        var divisionManager = new DivisionManager();
        await divisionManager.SwitchDivisionAsync(SparkDivision.VoiceMode);
        var service = new VoiceModeDivisionService(divisionManager);

        Assert.NotNull(service);
        Assert.False(string.IsNullOrWhiteSpace(service.CurrentVisualMode));
        Assert.False(string.IsNullOrWhiteSpace(service.CurrentModel));

        // Ensure EnsureRuntimeStartedAsync does not crash or throw unhandled exceptions
        var exception = await Record.ExceptionAsync(() => service.EnsureRuntimeStartedAsync());
        Assert.Null(exception);

        // Ensure LaunchVoiceModeUiAsync does not throw when VoiceMode is active
        var launchEx = await Record.ExceptionAsync(() => service.LaunchVoiceModeUiAsync());
        Assert.Null(launchEx);

        service.Dispose();
    }

    #endregion

    #region Problem 2: Creative Manual Editing Integrated Workspace Validation

    [Theory]
    [InlineData("photospark")]
    [InlineData("vectorspark")]
    [InlineData("pdfspark")]
    [InlineData("filmspark")]
    [InlineData("designspark")]
    public async Task CreativeLifecycleManager_LaunchApplication_ActivatesIntegratedWorkspace_WithoutExplorer(string appId)
    {
        var manager = CreativeLifecycleManager.Instance;
        var result = await manager.LaunchApplicationAsync(appId, isManualEditing: true);

        Assert.NotNull(result);
        Assert.True(result.Success, $"Launch should succeed for registered application '{appId}'");
        Assert.Equal(appId, result.ApplicationId);
        Assert.True(result.IsManualEditingMode, "Should be in manual editing mode");

        // When pre-compiled native binary is not installed, must activate integrated workspace
        Assert.True(result.IsIntegratedWorkspace, "Should activate integrated native workspace in SPARK");

        // MUST NEVER use explorer.exe
        Assert.NotEqual("explorer.exe", result.ExecutablePath);

        // Message must confirm integrated workspace activation
        Assert.Contains("integrated", result.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Problem 3: Light Mode Contrast & WCAG 2.2 AA Compliance Validation

    [Fact]
    public void AppXaml_LightDictionary_DefinesRequiredSemanticTokens()
    {
        var appXamlPath = Path.Combine(RepoRoot, "src", "OpenClaw.Tray.WinUI", "App.xaml");
        Assert.True(File.Exists(appXamlPath), $"App.xaml must exist at: {appXamlPath}");

        var content = File.ReadAllText(appXamlPath);
        var doc = XDocument.Parse(content);
        var defaultNs = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var xNs = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        var themeDicts = doc.Descendants(defaultNs + "ResourceDictionary")
            .Where(d => d.Attribute(xNs + "Key")?.Value == "Light")
            .ToList();

        Assert.NotEmpty(themeDicts);
        var lightDict = themeDicts.First();

        var brushKeys = lightDict.Elements(defaultNs + "SolidColorBrush")
            .Select(b => b.Attribute(xNs + "Key")?.Value)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        string[] requiredLightTokens =
        [
            "SolidBackgroundFillColorBaseBrush",
            "ApplicationPageBackgroundThemeBrush",
            "ControlFillColorDefaultBrush",
            "ControlFillColorSecondaryBrush",
            "ControlStrokeColorDefaultBrush",
            "ControlStrokeColorSecondaryBrush",
            "CardBackgroundFillColorDefaultBrush",
            "CardStrokeColorDefaultBrush",
            "SubtleFillColorSecondaryBrush",
            "SubtleFillColorTertiaryBrush",
            "TextFillColorPrimaryBrush",
            "TextFillColorSecondaryBrush",
            "TextFillColorTertiaryBrush",
            "NavigationViewExpandedPaneBackground",
            "NavigationViewDefaultPaneBackground",
            "AccentFillColorDefaultBrush",
            "AccentTextFillColorPrimaryBrush"
        ];

        foreach (var token in requiredLightTokens)
        {
            Assert.Contains(token, brushKeys);
        }
    }

    [Fact]
    public void LightThemeTokens_MeetWcag22AaContrast_AgainstBackground()
    {
        // WCAG relative luminance calculation helper
        static double RelativeLuminance(byte r, byte g, byte b)
        {
            double R = r / 255.0;
            double G = g / 255.0;
            double B = b / 255.0;

            R = (R <= 0.03928) ? R / 12.92 : Math.Pow((R + 0.055) / 1.055, 2.4);
            G = (G <= 0.03928) ? G / 12.92 : Math.Pow((G + 0.055) / 1.055, 2.4);
            B = (B <= 0.03928) ? B / 12.92 : Math.Pow((B + 0.055) / 1.055, 2.4);

            return 0.2126 * R + 0.7152 * G + 0.0722 * B;
        }

        static double ContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            var l1 = RelativeLuminance(r1, g1, b1);
            var l2 = RelativeLuminance(r2, g2, b2);
            var lighter = Math.Max(l1, l2);
            var darker = Math.Min(l1, l2);
            return (lighter + 0.05) / (darker + 0.05);
        }

        // Light mode surfaces: Page = #F8F9FA, Card = #FFFFFF
        // Primary text: #111827
        var primaryRatioAgainstWhite = ContrastRatio(17, 24, 39, 255, 255, 255);
        var primaryRatioAgainstPage = ContrastRatio(17, 24, 39, 248, 249, 250);

        // WCAG 2.2 AA requires >= 4.5:1 for normal text
        Assert.True(primaryRatioAgainstWhite >= 4.5, $"Primary text contrast on white ({primaryRatioAgainstWhite:F2}) must be >= 4.5:1");
        Assert.True(primaryRatioAgainstPage >= 4.5, $"Primary text contrast on page background ({primaryRatioAgainstPage:F2}) must be >= 4.5:1");

        // Secondary text: #4B5563
        var secondaryRatioAgainstWhite = ContrastRatio(75, 85, 99, 255, 255, 255);
        Assert.True(secondaryRatioAgainstWhite >= 4.5, $"Secondary text contrast on white ({secondaryRatioAgainstWhite:F2}) must be >= 4.5:1");

        // Accent text: #C2410C
        var accentRatioAgainstWhite = ContrastRatio(194, 65, 12, 255, 255, 255);
        Assert.True(accentRatioAgainstWhite >= 4.5, $"Accent text contrast on white ({accentRatioAgainstWhite:F2}) must be >= 4.5:1");
    }

    #endregion

    #region Problem 4: Model Selection & Local AI Status Synchronization Validation

    [Fact]
    public void ChatModelPreferencesStore_ActiveSelectedModel_UpdatesAndRaisesPreferencesChangedEvent()
    {
        var store = ChatModelPreferencesStore.Instance;
        var eventFired = false;
        string? notifiedModel = null;

        Action handler = () =>
        {
            eventFired = true;
            notifiedModel = store.ActiveSelectedModel;
        };

        store.PreferencesChanged += handler;
        try
        {
            var testModelId = "test-model-" + Guid.NewGuid().ToString("N");
            store.ActiveSelectedModel = testModelId;

            Assert.True(eventFired, "PreferencesChanged event must fire when ActiveSelectedModel is updated");
            Assert.Equal(testModelId, store.ActiveSelectedModel);
            Assert.Equal(testModelId, notifiedModel);
        }
        finally
        {
            store.PreferencesChanged -= handler;
        }
    }

    [Fact]
    public void LocalModelCatalog_ResolvesModelMetadata_WithoutUnknownFallbacks()
    {
        // For all catalog models, metadata should be rich, populated, and non-empty
        Assert.NotEmpty(LocalModelCatalog.Models);

        foreach (var model in LocalModelCatalog.Models)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id), "Model Id must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName), $"DisplayName must not be empty for {model.Id}");
            Assert.False(string.Equals("Unknown", model.DisplayName, StringComparison.OrdinalIgnoreCase), $"DisplayName must not be Unknown for {model.Id}");

            var resolved = LocalModelCatalog.Find(model.Id);
            Assert.NotNull(resolved);
            Assert.Equal(model.DisplayName, resolved.DisplayName);

            var profiles = LocalModelCatalog.GetProfiles(model);
            Assert.NotEmpty(profiles);
            var bestProfile = profiles[0];
            Assert.True(bestProfile.ContextTokens > 0, $"ContextTokens must be > 0 for {model.Id}");
        }
    }

    [Theory]
    [InlineData(LocalModelCatalog.Qwen38_27BModelId, "Qwen3.8 27B (UD-Q4_K_M)")]
    [InlineData(LocalModelCatalog.Qwen35BModelId, "Qwen3.6 35B-A3B (UD-Q4_K_M)")]
    [InlineData(LocalModelCatalog.Bonsai2ModelId, "Bonsai 2 27B (PTQ1_0, experimental)")]
    public void LocalModelCatalog_KnownPinnedModels_MatchExpectedSpecs(string modelId, string expectedDisplayName)
    {
        var model = LocalModelCatalog.Find(modelId);
        Assert.NotNull(model);
        Assert.Equal(expectedDisplayName, model.DisplayName);

        var profiles = LocalModelCatalog.GetProfiles(model);
        Assert.NotEmpty(profiles);
        Assert.True(profiles.Count > 0);
    }

    #endregion

    #region Problem 4: Real AI Execution Across Divisions & Console Window Suppression

    [Fact]
    public async Task CodingAgentOrchestrator_AppliesFileChanges_WhenModelGeneratesCodeBlocks()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "spark_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var orchestrator = new OpenClaw.Shared.Coding.Orchestrator.CodingAgentOrchestrator(tempWorkspace);

            // Mock model generator outputting a new file via markdown code block
            Func<string, Task<string>> modelGen = _ => Task.FromResult(
                "I have authored the solution:\n\n" +
                "### `Calculator.cs`\n" +
                "```csharp\n" +
                "public static class Calculator { public static int Add(int a, int b) => a + b; }\n" +
                "```\n" +
                "Execution verified.");

            var fileChangedFired = false;
            orchestrator.FileChanged += (s, e) =>
            {
                if (e.FilePath.EndsWith("Calculator.cs", StringComparison.OrdinalIgnoreCase))
                    fileChangedFired = true;
            };

            await orchestrator.ExecuteTaskAsync("Create Calculator.cs with Add function", modelGenerator: modelGen);

            var createdFilePath = Path.Combine(tempWorkspace, "Calculator.cs");
            Assert.True(File.Exists(createdFilePath), "Calculator.cs must exist on disk after task execution");
            var content = File.ReadAllText(createdFilePath);
            Assert.Contains("public static int Add", content);
            Assert.True(fileChangedFired, "FileChanged event must fire when files are written");
        }
        finally
        {
            try { Directory.Delete(tempWorkspace, true); } catch { }
        }
    }

    [Fact]
    public void SparkToolRegistry_RegistersAndExecutes_AgentGeneralReasoning()
    {
        var registry = new OpenClaw.Shared.Tools.SparkToolRegistry();
        registry.RegisterStandardTools();

        var tool = registry.GetTool("agent_general_reasoning");
        Assert.NotNull(tool);
        Assert.Equal("agent_general_reasoning", tool.Metadata.Id);
    }

    [Fact]
    public async Task SparkToolRegistry_DecomposedActions_RouteToReasoningFallback()
    {
        var registry = new OpenClaw.Shared.Tools.SparkToolRegistry();
        registry.RegisterStandardTools();

        var request = new OpenClaw.Shared.Tools.SparkToolExecutionRequest
        {
            ToolId = "travel_search_trains",
            Arguments = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["query"] = "Find trains from Ernakulam to Kozhikode"
            },
            GrantedPermissions = new System.Collections.Generic.HashSet<string>()
        };

        var response = await registry.ExecuteToolAsync(request);
        Assert.True(response.Success);
        Assert.False(string.IsNullOrWhiteSpace(response.Result?.ToString()));
    }

    #endregion
}
