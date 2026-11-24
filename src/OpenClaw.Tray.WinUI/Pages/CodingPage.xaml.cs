using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Shared.Coding;
using OpenClaw.Shared.Coding.BuildAnything;
using OpenClaw.Shared.Coding.CliIntelligence;
using OpenClaw.Shared.Coding.CyberSecurity;
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.Permissions;
using OpenClaw.Shared.Coding.ReverseEngineering;
using OpenClaw.Shared.Commands;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OpenClawTray.Pages;

public sealed partial class CodingPage : Page
{
    private static App CurrentApp => (App)Microsoft.UI.Xaml.Application.Current!;
    private readonly ICodingDivisionService _codingService;
    private TaskCompletionSource<(bool Approved, bool Remember)>? _pendingApprovalTcs;
    private FileChangeRecord? _selectedChange;
    private SecurityFinding? _selectedFinding;
    private CancellationTokenSource? _activeTaskCts;
    private bool _workspaceInitialized;

    public CodingPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

        _codingService = new CodingDivisionService();
        _codingService.ActivityChanged += OnActivityChanged;
        _codingService.PlanUpdated += OnPlanUpdated;
        _codingService.FileChanged += OnFileChanged;
        _codingService.MessageAppended += OnMessageAppended;

        if (_codingService is CodingDivisionService concreteService)
        {
            concreteService.PermissionManager.UserPromptHandler = PromptUserApprovalAsync;
        }

        // Initialize CLI Harness Selector
        PopulateCliHarnesses();

        // Wire Cybersecurity Engine Events
        _codingService.AuditEngine.PhaseChanged += OnAuditPhaseChanged;
        _codingService.AuditEngine.AgentStatusChanged += OnAuditAgentStatusChanged;
        _codingService.AuditEngine.FindingDiscovered += OnAuditFindingDiscovered;
        _codingService.AuditEngine.AuditCompleted += OnAuditCompleted;

        // Initialize with default workspace if available
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (_workspaceInitialized) return;
        _workspaceInitialized = true;
        var defaultPath = @"C:\Intel\Spark\Spark-main";
        if (Directory.Exists(defaultPath))
        {
            await OpenWorkspaceInternalAsync(defaultPath);
        }
    }

    private void PopulateCliHarnesses()
    {
        if (HarnessCategoryCombo != null)
        {
            HarnessCategoryCombo.Items.Clear();
            HarnessCategoryCombo.Items.Add(new ComboBoxItem { Content = $"All Hub Tools ({_codingService.CliEngine.HarnessRegistry.Count})", Tag = null });

            // Add capability matrices
            foreach (var m in _codingService.CliEngine.Matrices)
            {
                HarnessCategoryCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"⚡ Matrix: {m.Name} ({m.Clis.Count})",
                    Tag = $"matrix:{m.Id}"
                });
            }

            // Add categories
            var categories = OpenClaw.Shared.Coding.CliIntelligence.SparkCliHubCatalog.GetCategories();
            foreach (var cat in categories)
            {
                var count = _codingService.CliEngine.HarnessRegistry.Count(h => string.Equals(h.Category, cat, StringComparison.OrdinalIgnoreCase));
                HarnessCategoryCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{char.ToUpper(cat[0])}{cat[1..]} ({count})",
                    Tag = cat
                });
            }
            HarnessCategoryCombo.SelectedIndex = 0;
        }

        UpdateHarnessList(null);
    }

    private void UpdateHarnessList(string? filterTag)
    {
        if (HarnessSelectorCombo == null) return;
        HarnessSelectorCombo.Items.Clear();

        IEnumerable<CliHarnessEntry> harnesses = _codingService.CliEngine.HarnessRegistry;

        if (!string.IsNullOrEmpty(filterTag))
        {
            if (filterTag.StartsWith("matrix:"))
            {
                var matrixId = filterTag["matrix:".Length..];
                harnesses = _codingService.CliEngine.FilterByMatrix(matrixId);
            }
            else
            {
                harnesses = _codingService.CliEngine.FilterByCategory(filterTag);
            }
        }

        foreach (var harness in harnesses.OrderBy(h => h.DisplayName))
        {
            HarnessSelectorCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{harness.DisplayName} [{harness.Category}]",
                Tag = harness
            });
        }

        if (HarnessSelectorCombo.Items.Count > 0)
        {
            HarnessSelectorCombo.SelectedIndex = 0;
        }
    }

    private async Task OpenWorkspaceInternalAsync(string path)
    {
        try
        {
            ActivityProgressBar.Visibility = Visibility.Visible;
            var analysis = await _codingService.OpenWorkspaceAsync(path);

            ProjectPathText.Text = analysis.ProjectName;
            ToolTipService.SetToolTip(ProjectPathText, analysis.ProjectRoot);

            if (analysis.DetectedLanguages.Count > 0)
            {
                TechStackBadge.Visibility = Visibility.Visible;
                TechStackText.Text = string.Join(" · ", analysis.DetectedLanguages.Take(2));
            }

            if (!string.IsNullOrEmpty(analysis.GitBranch))
            {
                GitBranchBadge.Visibility = Visibility.Visible;
                GitBranchText.Text = $"{analysis.GitBranch} {(analysis.GitIsDirty ? "*" : "")}";
            }

            if (AuditRepoPathBox != null)
            {
                AuditRepoPathBox.Text = analysis.ProjectRoot;
            }

            AddSystemMessage($"Opened project: {analysis.ProjectName} ({analysis.BuildSystem}) with {analysis.TotalSourceFiles} source files.");
        }
        catch (Exception ex)
        {
            AddSystemMessage($"Failed to open workspace: {ex.Message}");
        }
        finally
        {
            ActivityProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            var window = CurrentApp.ActiveHubWindow;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                await OpenWorkspaceInternalAsync(folder.Path);
            }
        }
        catch
        {
            var fallback = @"C:\Intel\Spark\Spark-main";
            if (Directory.Exists(fallback))
            {
                await OpenWorkspaceInternalAsync(fallback);
            }
        }
    }

    private async void OnAnalyzeClicked(object sender, RoutedEventArgs e)
    {
        if (_codingService.CurrentWorkspaceRoot == null) return;
        await OpenWorkspaceInternalAsync(_codingService.CurrentWorkspaceRoot);
    }

    private async void OnRunTestsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            ActivityProgressBar.Visibility = Visibility.Visible;
            TabTerminal.IsChecked = true;
            AppendTerminal($"Running tests for {_codingService.CurrentWorkspaceRoot}...\n");

            var testRes = await _codingService.RunTestsAsync();
            AppendTerminal(testRes.RawOutput + "\n");

            var summary = testRes.Success
                ? $"Tests Passed: {testRes.TotalTests} tests passed cleanly ({testRes.DurationSeconds:N2}s)."
                : $"Tests Failed: {testRes.FailedCount}/{testRes.TotalTests} failed.";

            AddSystemMessage(summary);
        }
        catch (Exception ex)
        {
            AppendTerminal($"Test run error: {ex.Message}\n");
        }
        finally
        {
            ActivityProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnGitStatusClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            TabTerminal.IsChecked = true;
            var (modified, untracked, staged, raw) = await _codingService.GetGitStatusAsync();
            AppendTerminal("=== GIT STATUS ===\n" + raw + "\n");

            var diff = await _codingService.GetGitDiffAsync();
            if (!string.IsNullOrEmpty(diff))
            {
                DiffFileTitle.Text = "Git Uncommitted Diff";
                DiffStatsText.Text = $"Modified: {modified.Count}, Untracked: {untracked.Count}";
                DiffViewerText.Text = diff;
                TabDiff.IsChecked = true;
            }
        }
        catch (Exception ex)
        {
            AppendTerminal($"Git status error: {ex.Message}\n");
        }
    }

    private void OnReviewClicked(object sender, RoutedEventArgs e)
    {
        TabReview.IsChecked = true;
        var orchestrator = _codingService.CurrentOrchestrator;
        if (orchestrator != null && orchestrator.ActiveSession.ModifiedFiles.Count > 0)
        {
            var report = orchestrator.Reviewer.ReviewChanges(orchestrator.ActiveSession.ModifiedFiles);
            ReviewSummaryText.Text = report.Summary;
            ReviewScoreText.Text = $"Score: {report.QualityScore:N0} / 100 ({report.ErrorCount} errors, {report.WarningCount} warnings)";

            ReviewFindingsPanel.Children.Clear();
            foreach (var finding in report.Findings)
            {
                var border = new Border
                {
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var sp = new StackPanel { Spacing = 4 };
                sp.Children.Add(new TextBlock
                {
                    Text = $"[{finding.Severity}] {finding.Title} ({finding.FilePath})",
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    Foreground = finding.Severity == CodingDiagnosticSeverity.Error
                        ? new SolidColorBrush(Colors.IndianRed)
                        : new SolidColorBrush(ColorHelper.FromArgb(255, 217, 119, 6)) // Amber-600 WCAG AA
                });
                sp.Children.Add(new TextBlock
                {
                    Text = finding.Description,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                });
                if (!string.IsNullOrEmpty(finding.Recommendation))
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = $"Recommendation: {finding.Recommendation}",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                    });
                }

                border.Child = sp;
                ReviewFindingsPanel.Children.Add(border);
            }
        }
        else
        {
            ReviewSummaryText.Text = "No files have been modified yet in this session.";
            ReviewScoreText.Text = "Score: 100 / 100";
            ReviewFindingsPanel.Children.Clear();
        }
    }

    // ── Prompt & Slash Command Handling ──
    private async void OnSendClicked(object sender, RoutedEventArgs e)
    {
        var prompt = PromptInputBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        PromptInputBox.Text = string.Empty;
        SendButton.IsEnabled = false;
        ActivityProgressBar.Visibility = Visibility.Visible;

        // Check if user invoked a registered SPARK slash command
        if (prompt.StartsWith("/"))
        {
            await HandleSlashCommandAsync(prompt);
            SendButton.IsEnabled = true;
            ActivityProgressBar.Visibility = Visibility.Collapsed;
            return;
        }

        _activeTaskCts = new CancellationTokenSource();

        try
        {
            var modelRouter = CurrentApp.Services?.GetService(typeof(OpenClaw.Shared.Inference.Routing.ISparkModelRouter)) as OpenClaw.Shared.Inference.Routing.ISparkModelRouter;
            Func<string, Task<string>>? modelGenerator = null;

            if (modelRouter != null)
            {
                modelGenerator = async (taskPrompt) =>
                {
                    try
                    {
                        var req = new OpenClaw.Shared.Inference.Routing.SparkRoutingRequest
                        {
                            TaskCategory = OpenClaw.Shared.Inference.Routing.SparkTaskCategory.Coding,
                            PrivacyMode = OpenClaw.Shared.Inference.Routing.SparkModelPrivacyMode.PreferLocal,
                            ContainsSensitiveLocalData = false
                        };
                        var invoker = new Func<OpenClaw.Shared.Inference.Routing.SparkModelDescriptor, CancellationToken, Task<string>>(
                            async (desc, cancelToken) =>
                            {
                                using var ollama = new OpenClaw.Shared.Inference.OllamaInferenceBackend();
                                var chatRes = await ollama.ChatAsync(new OpenClaw.Shared.Inference.LocalInferenceChatRequest(
                                    Model: desc.ModelId ?? "qwen2.5-coder:7b",
                                    Prompt: taskPrompt,
                                    System: "You are an autonomous senior software engineer. When writing, fixing, or modifying code, clearly specify the file path with a header (e.g. '### path/to/file.ext') followed by the markdown code block containing the complete code. Provide robust, production-ready code.",
                                    Temperature: 0.2,
                                    MaxTokens: 2048,
                                    TimeoutMs: 60000
                                ), cancelToken);
                                return chatRes.Response ?? "No completion output generated.";
                            });

                        var result = await modelRouter.ExecuteWithFallbackAsync(req, invoker, _activeTaskCts?.Token ?? default);
                        return result.Content ?? "Task step completed successfully.";
                    }
                    catch (Exception ex)
                    {
                        return $"[Inference Failed: {ex.Message}. Verify Ollama is running at http://127.0.0.1:11434 with a downloaded model.]";
                    }
                };
            }

            await _codingService.SendPromptAsync(prompt, modelGenerator: modelGenerator, ct: _activeTaskCts.Token);
        }
        catch (Exception ex)
        {
            AddSystemMessage($"Task error: {ex.Message}");
        }
        finally
        {
            SendButton.IsEnabled = true;
            ActivityProgressBar.Visibility = Visibility.Collapsed;
            _activeTaskCts?.Dispose();
            _activeTaskCts = null;
        }
    }

    private async Task HandleSlashCommandAsync(string commandText)
    {
        var execContext = new SparkCommandExecutionContext
        {
            WorkspacePath = _codingService.CurrentWorkspaceRoot,
            SessionKey = _codingService.CurrentSession?.SessionId ?? "coding-division"
        };

        var result = await SparkSlashCommandRegistry.Instance.ExecuteLineAsync(commandText, execContext);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(result.OutputMessage))
            {
                AddSystemMessage(result.OutputMessage);
            }

            // Route client actions
            switch (result.ClientAction)
            {
                case "StartCyberSecurityAudit":
                    TabAudit.IsChecked = true;
                    OnTabSelectionChanged(this, new RoutedEventArgs());
                    if (!string.IsNullOrWhiteSpace(result.ActionPayload) && result.ActionPayload != "active project")
                    {
                        AuditTargetUrlBox.Text = result.ActionPayload;
                    }
                    if (AuditTargetUrlBox.Text.Length > 0)
                    {
                        OnStartAuditClicked(this, new RoutedEventArgs());
                    }
                    break;

                case "StartBuildAnything":
                    TabBuild.IsChecked = true;
                    OnTabSelectionChanged(this, new RoutedEventArgs());
                    if (!string.IsNullOrWhiteSpace(result.ActionPayload))
                    {
                        BuildPromptBox.Text = result.ActionPayload;
                        OnBuildProjectClicked(this, new RoutedEventArgs());
                    }
                    break;

                case "OpenCliIntelligence":
                    TabTerminal.IsChecked = true;
                    OnTabSelectionChanged(this, new RoutedEventArgs());
                    if (!string.IsNullOrWhiteSpace(result.ActionPayload))
                    {
                        TerminalInputBox.Text = result.ActionPayload;
                    }
                    break;

                case "StartReverseEngineering":
                    TabReverse.IsChecked = true;
                    OnTabSelectionChanged(this, new RoutedEventArgs());
                    if (!string.IsNullOrWhiteSpace(result.ActionPayload))
                    {
                        ReverseTargetPathBox.Text = result.ActionPayload;
                        OnInspectTargetClicked(this, new RoutedEventArgs());
                    }
                    break;
            }
        }
        else
        {
            AddSystemMessage($"Command error: {result.ErrorMessage ?? "Execution failed"}");
        }
    }

    private void OnPromptKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            OnSendClicked(sender, e);
        }
    }

    private void OnQuickPromptClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string text)
        {
            PromptInputBox.Text = text;
            OnSendClicked(sender, e);
        }
    }

    private void OnTabSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (ChatViewHost == null) return;

        ChatViewHost.Visibility = TabChat.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        BuildViewHost.Visibility = TabBuild.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ReverseViewHost.Visibility = TabReverse.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TerminalViewHost.Visibility = TabTerminal.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AuditViewHost.Visibility = TabAudit.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DiffViewHost.Visibility = TabDiff.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ReviewViewHost.Visibility = TabReview.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Build Anything (Craft Anything) Event Handlers ──
    private async void OnBuildProjectClicked(object sender, RoutedEventArgs e)
    {
        var prompt = BuildPromptBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        var projName = "SparkApp_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var workRoot = _codingService.CurrentWorkspaceRoot ?? @"C:\Intel\Spark\Spark-main";
        var targetDir = Path.Combine(workRoot, "GeneratedProjects", projName);

        var spec = new CraftProjectSpec
        {
            Name = projName,
            Description = prompt,
            TargetType = (CraftProjectType)ProjectTypeCombo.SelectedIndex,
            TargetDirectory = targetDir
        };

        BtnGenerateProject.IsEnabled = false;
        ActivityProgressBar.Visibility = Visibility.Visible;

        try
        {
            var session = await _codingService.CraftEngine.CreateProjectAsync(spec);
            BuildEmptyFilesText.Visibility = Visibility.Collapsed;
            BuildGeneratedFilesPanel.Children.Clear();

            foreach (var f in session.GeneratedFiles)
            {
                var fileItem = new Border
                {
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6)
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                sp.Children.Add(new FontIcon { Glyph = "\uE8B7", FontSize = 12 });
                sp.Children.Add(new TextBlock { Text = f.RelativePath, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
                sp.Children.Add(new TextBlock { Text = $"({f.SizeBytes} bytes - {f.Purpose})", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                fileItem.Child = sp;
                BuildGeneratedFilesPanel.Children.Add(fileItem);
            }

            if (session.Output != null && session.Output.BuildSuccess)
            {
                BuildOutputBanner.Visibility = Visibility.Visible;
                BuildOutputSummaryText.Text = $"Build Output Discovered: {session.Output.OutputType}";
                BuildOutputDetailsText.Text = session.Output.SiteDirectory ?? session.Output.BuiltExecutablePath ?? targetDir;
            }

            AddSystemMessage($"Build Anything: Successfully scaffolded '{projName}' into {targetDir}.");
        }
        catch (Exception ex)
        {
            AddSystemMessage($"Build Anything error: {ex.Message}");
        }
        finally
        {
            BtnGenerateProject.IsEnabled = true;
            ActivityProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void OnOpenBuildOutputFolderClicked(object sender, RoutedEventArgs e)
    {
        var session = _codingService.Context.ActiveBuildSession;
        var dir = session?.Spec.TargetDirectory ?? _codingService.CurrentWorkspaceRoot;
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
    }

    private void OnLaunchBuildPreviewClicked(object sender, RoutedEventArgs e)
    {
        var output = _codingService.Context.ActiveBuildSession?.Output;
        var url = output?.PreviewUrl;
        if (!string.IsNullOrEmpty(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (!string.IsNullOrEmpty(output?.BuiltExecutablePath) && File.Exists(output.BuiltExecutablePath))
        {
            Process.Start(new ProcessStartInfo(output.BuiltExecutablePath) { UseShellExecute = true });
        }
    }

    // ── Reverse Engineering (Rea) Event Handlers ──
    private async void OnBrowseReverseTargetClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add(".dll");
            picker.FileTypeFilter.Add(".exe");
            picker.FileTypeFilter.Add(".asar");
            picker.FileTypeFilter.Add(".apk");
            picker.FileTypeFilter.Add(".js");
            picker.FileTypeFilter.Add("*");

            var window = CurrentApp.ActiveHubWindow;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ReverseTargetPathBox.Text = file.Path;
            }
        }
        catch
        {
            // Dialog fallback
        }
    }

    private async void OnInspectTargetClicked(object sender, RoutedEventArgs e)
    {
        var path = ReverseTargetPathBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            path = _codingService.CurrentWorkspaceRoot ?? @"C:\Intel\Spark\Spark-main";
        }

        BtnInspectTarget.IsEnabled = false;
        ActivityProgressBar.Visibility = Visibility.Visible;

        try
        {
            var result = await _codingService.ReverseEngine.InspectTargetAsync(path);
            ReverseEmptyEvidenceText.Visibility = Visibility.Collapsed;
            ReverseEvidencePanel.Children.Clear();

            foreach (var ev in result.Evidence)
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                var sp = new StackPanel { Spacing = 4 };
                sp.Children.Add(new TextBlock
                {
                    Text = $"[{ev.Id}] {ev.Category} · {ev.Confidence} Confidence",
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                });
                sp.Children.Add(new TextBlock
                {
                    Text = ev.Summary,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    TextWrapping = TextWrapping.Wrap
                });
                if (!string.IsNullOrEmpty(ev.CodeSnippet))
                {
                    var snippetBox = new Border
                    {
                        Background = new SolidColorBrush(ColorHelper.FromArgb(255, 13, 14, 16)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    snippetBox.Child = new TextBlock
                    {
                        Text = ev.CodeSnippet,
                        Style = (Style)Resources["MonospaceTextStyle"],
                        Foreground = new SolidColorBrush(Colors.LightGray),
                        TextWrapping = TextWrapping.Wrap
                    };
                    sp.Children.Add(snippetBox);
                }

                card.Child = sp;
                ReverseEvidencePanel.Children.Add(card);
            }

            ReverseSpecText.Text = result.CleanRoomSpecification;
            AddSystemMessage($"Reverse Engineering: Inspected '{result.TargetName}' ({result.Category}) and extracted {result.Evidence.Count} evidence records.");
        }
        catch (Exception ex)
        {
            AddSystemMessage($"Reverse Engineering error: {ex.Message}");
        }
        finally
        {
            BtnInspectTarget.IsEnabled = true;
            ActivityProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    // ── CLI & Terminal Intelligence (CLI Anything) Handlers ──
    private void OnHarnessCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HarnessCategoryCombo?.SelectedItem is ComboBoxItem item)
        {
            var filterTag = item.Tag as string;
            UpdateHarnessList(filterTag);
        }
    }

    private void OnHarnessSelected(object sender, SelectionChangedEventArgs e)
    {
        if (HarnessSelectorCombo?.SelectedItem is ComboBoxItem item && item.Tag is CliHarnessEntry harness)
        {
            TerminalInputBox.Text = $"{harness.EntryPoint} --help";
            if (HarnessDetailsText != null)
            {
                var req = string.IsNullOrEmpty(harness.Requires) || harness.Requires == "None" ? "" : $" | Requires: {harness.Requires}";
                HarnessDetailsText.Text = $"• {harness.DisplayName} [{harness.Category}] — {harness.Description}{req}";
                HarnessDetailsText.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnNlToCliClicked(object sender, RoutedEventArgs e)
    {
        var query = NlToCliBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        var cmd = _codingService.CliEngine.TranslateNaturalLanguageToCli(query);
        TerminalInputBox.Text = cmd;
    }

    private void OnNlToCliKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            OnNlToCliClicked(sender, e);
        }
    }

    // ── Cybersecurity Audit (Shannon) Event Handlers ──
    private async void OnStartAuditClicked(object sender, RoutedEventArgs e)
    {
        var targetUrl = AuditTargetUrlBox.Text.Trim();
        if (string.IsNullOrEmpty(targetUrl))
        {
            targetUrl = "http://localhost:3000";
            AuditTargetUrlBox.Text = targetUrl;
        }

        var repoPath = AuditRepoPathBox.Text.Trim();
        if (string.IsNullOrEmpty(repoPath))
        {
            repoPath = _codingService.CurrentWorkspaceRoot ?? @"C:\Intel\Spark\Spark-main";
            AuditRepoPathBox.Text = repoPath;
        }

        var isAuthorized = AuditAuthCheck.IsChecked == true;
        if (!isAuthorized)
        {
            AddSystemMessage("Cybersecurity Audit requires explicit confirmation that the target scope is authorized.");
            return;
        }

        var options = new SecurityAuditOptions
        {
            TargetUrl = targetUrl,
            TargetRepository = repoPath,
            IsAuthorizedTarget = true,
            MinSeverity = AuditSeverity.Low
        };

        BtnStartAudit.IsEnabled = false;
        ActivityProgressBar.Visibility = Visibility.Visible;
        AuditPhaseProgressBar.Value = 10;
        AuditPhaseLabel.Text = "Pipeline: Starting Pre-Reconnaissance...";
        AuditEmptyFindingsText.Visibility = Visibility.Collapsed;
        AuditFindingsPanel.Children.Clear();

        try
        {
            await _codingService.AuditEngine.StartAuditAsync(options);
        }
        catch (Exception ex)
        {
            AddSystemMessage($"Cybersecurity Audit error: {ex.Message}");
        }
        finally
        {
            BtnStartAudit.IsEnabled = true;
            ActivityProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void OnAuditPhaseChanged(object? sender, AuditPhase phase)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AuditPhaseLabel.Text = $"Pipeline Phase: {phase}";
            AuditPhaseProgressBar.Value = phase switch
            {
                AuditPhase.PreRecon => 20,
                AuditPhase.Recon => 40,
                AuditPhase.VulnIdentification => 60,
                AuditPhase.ExploitationVerification => 80,
                AuditPhase.ReportAuthoring => 95,
                AuditPhase.Completed => 100,
                _ => 0
            };
        });
    }

    private void OnAuditAgentStatusChanged(object? sender, SecurityAgentState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Dynamically update agent card in AuditAgentsPanel
            var existingCard = AuditAgentsPanel.Children
                .OfType<Border>()
                .FirstOrDefault(b => (string?)b.Tag == state.AgentId);

            if (existingCard == null)
            {
                var card = new Border
                {
                    Tag = state.AgentId,
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 4, 8, 4)
                };
                var sp = new StackPanel { Spacing = 2 };
                var titleText = new TextBlock { Text = state.DisplayName, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                var statusText = new TextBlock { Text = state.Status.ToString(), Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
                sp.Children.Add(titleText);
                sp.Children.Add(statusText);
                card.Child = sp;
                AuditAgentsPanel.Children.Add(card);
            }
            else if (existingCard.Child is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock tb)
            {
                tb.Text = $"{state.Status} {(state.FindingsCount > 0 ? $"({state.FindingsCount})" : "")}";
                tb.Foreground = state.Status == AuditAgentStatus.Completed
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 5, 150, 105)) // Emerald green WCAG AA
                    : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            }
        });
    }

    private void OnAuditFindingDiscovered(object? sender, SecurityFinding finding)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 4),
                Style = (Style)Application.Current.Resources["SubtleButtonStyle"]
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var badgeColor = finding.Severity switch
            {
                AuditSeverity.Critical => Colors.IndianRed,
                AuditSeverity.High => Colors.OrangeRed,
                AuditSeverity.Medium => ColorHelper.FromArgb(255, 217, 119, 6),
                _ => Colors.DeepSkyBlue
            };

            var badge = new Border
            {
                Background = new SolidColorBrush(badgeColor),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock { Text = finding.Severity.ToString().ToUpperInvariant(), FontSize = 10, Foreground = new SolidColorBrush(Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.Bold }
            };
            Grid.SetColumn(badge, 0);

            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = $"{finding.Id}: {finding.Title}", Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(new TextBlock { Text = finding.VulnerableEndpoint, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(sp, 1);

            grid.Children.Add(badge);
            grid.Children.Add(sp);
            btn.Content = grid;

            btn.Click += (_, _) =>
            {
                _selectedFinding = finding;
                FindingDetailTitle.Text = $"[{finding.Severity}] {finding.Id}: {finding.Title}";
                FindingDetailSummary.Text = finding.Summary;
                FindingDetailEndpoint.Text = $"Endpoint: {finding.VulnerableEndpoint} {(finding.SourceFile != null ? $"· Source: {finding.SourceFile}:{finding.SourceLine}" : "")}";
                FindingDetailPayload.Text = finding.RawPayload ?? "No raw payload";
                FindingDetailImpact.Text = finding.ProofOfImpact;
                FindingDetailRemediation.Text = finding.Remediation;
            };

            AuditFindingsPanel.Children.Add(btn);
            AuditStatsText.Text = $"{_codingService.AuditEngine.ActiveSession?.TotalFindings ?? 1} Verified Findings";
        });
    }

    private void OnAuditCompleted(object? sender, SecurityAuditSession session)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AuditPhaseProgressBar.Value = 100;
            AuditPhaseLabel.Text = "Pipeline Status: Assessment Complete (Report Ready)";
            AddSystemMessage($"Cybersecurity Audit: Completed with {session.TotalFindings} verified findings. Markdown and SARIF reports authored.");
        });
    }

    private void OnExportReportMdClicked(object sender, RoutedEventArgs e)
    {
        var session = _codingService.AuditEngine.ActiveSession;
        if (session != null && !string.IsNullOrEmpty(session.ReportMarkdown))
        {
            DiffFileTitle.Text = "Cybersecurity Assessment Report (Markdown)";
            DiffStatsText.Text = $"{session.TotalFindings} Findings";
            DiffViewerText.Text = session.ReportMarkdown;
            TabDiff.IsChecked = true;
        }
        else
        {
            AddSystemMessage("No cybersecurity report generated yet. Launch an audit first.");
        }
    }

    private void OnExportReportSarifClicked(object sender, RoutedEventArgs e)
    {
        var session = _codingService.AuditEngine.ActiveSession;
        if (session != null && !string.IsNullOrEmpty(session.ReportSarif))
        {
            DiffFileTitle.Text = "Cybersecurity Assessment Report (SARIF 2.1.0)";
            DiffStatsText.Text = "SARIF JSON";
            DiffViewerText.Text = session.ReportSarif;
            TabDiff.IsChecked = true;
        }
        else
        {
            AddSystemMessage("No SARIF report generated yet. Launch an audit first.");
        }
    }

    // ── Implementation Plan & File Change Binding ──
    private void OnPlanUpdated(object? sender, CodingPlan plan)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PlanProgressText.Text = $"{plan.CompletedStepCount}/{plan.TotalStepCount} ({plan.ProgressPercent:N0}%)";
            PlanProgressBar.Value = plan.ProgressPercent;
            EmptyPlanText.Visibility = Visibility.Collapsed;

            PlanStepsPanel.Children.Clear();
            foreach (var step in plan.Steps)
            {
                var stepGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                stepGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                stepGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var icon = new FontIcon
                {
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                switch (step.Status)
                {
                    case PlanStepStatus.Completed:
                        icon.Glyph = "\uE73E"; // Checkmark
                        icon.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 5, 150, 105)); // Emerald green WCAG AA
                        break;
                    case PlanStepStatus.InProgress:
                        icon.Glyph = "\uE768"; // Play
                        icon.Foreground = new SolidColorBrush(Colors.DeepSkyBlue);
                        break;
                    case PlanStepStatus.Failed:
                        icon.Glyph = "\uE711"; // Cross
                        icon.Foreground = new SolidColorBrush(Colors.IndianRed);
                        break;
                    default:
                        icon.Glyph = "\uE916"; // Stopwatch
                        icon.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                        break;
                }

                Grid.SetColumn(icon, 0);

                var sp = new StackPanel { Spacing = 2 };
                sp.Children.Add(new TextBlock
                {
                    Text = step.Title,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                });
                if (!string.IsNullOrEmpty(step.ResultSummary))
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = step.ResultSummary,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                Grid.SetColumn(sp, 1);

                stepGrid.Children.Add(icon);
                stepGrid.Children.Add(sp);

                PlanStepsPanel.Children.Add(stepGrid);
            }
        });
    }

    private void OnFileChanged(object? sender, FileChangeRecord change)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            EmptyFilesText.Visibility = Visibility.Collapsed;
            var orchestrator = _codingService.CurrentOrchestrator;
            var count = orchestrator?.ActiveSession.ModifiedFiles.Count ?? 1;
            ModifiedFilesHeader.Text = $"Modified Files ({count})";

            var fileBtn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 2),
                Style = (Style)Application.Current.Resources["SubtleButtonStyle"]
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = change.FilePath,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 0);

            var diffText = change.Diff != null
                ? $"+{change.Diff.Additions} -{change.Diff.Deletions}"
                : "mod";

            var badge = new TextBlock
            {
                Text = diffText,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 5, 150, 105)) // Emerald green WCAG AA
            };
            Grid.SetColumn(badge, 1);

            grid.Children.Add(nameBlock);
            grid.Children.Add(badge);
            fileBtn.Content = grid;

            fileBtn.Click += (_, _) =>
            {
                _selectedChange = change;
                DiffFileTitle.Text = change.FilePath;
                DiffStatsText.Text = diffText;
                DiffViewerText.Text = change.Diff?.RawDiffText ?? change.NewContent ?? "Empty";
                RevertFileButton.Visibility = Visibility.Visible;
                TabDiff.IsChecked = true;
            };

            ModifiedFilesPanel.Children.Add(fileBtn);
        });
    }

    private async void OnRevertFileClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedChange == null) return;
        var success = await _codingService.RollbackChangeAsync(_selectedChange);
        if (success)
        {
            DiffViewerText.Text = $"[Reverted file: {_selectedChange.FilePath}]";
            RevertFileButton.Visibility = Visibility.Collapsed;
            AddSystemMessage($"Reverted change to {_selectedChange.FilePath}");
        }
    }

    private void OnMessageAppended(object? sender, CodingMessage msg)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = msg.Role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                MaxWidth = msg.Role == "user" ? 600 : double.PositiveInfinity,
                Background = msg.Role == "user"
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1)
            };

            var sp = new StackPanel { Spacing = 6 };
            sp.Children.Add(new TextBlock
            {
                Text = msg.Role == "user" ? "You" : "SPARK Coding Agent",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = msg.Role == "user" ? new SolidColorBrush(Colors.White) : (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            });
            sp.Children.Add(new TextBlock
            {
                Text = msg.Content,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                Foreground = msg.Role == "user" ? new SolidColorBrush(Colors.White) : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });

            bubble.Child = sp;
            MessagesPanel.Children.Add(bubble);
            ConversationScrollViewer.ChangeView(null, ConversationScrollViewer.ScrollableHeight, null);
        });
    }

    private void OnActivityChanged(object? sender, CodingAgentActivity activity)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (activity.Type == CodingAgentActivityType.Completed)
            {
                ActivityProgressBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                ActivityProgressBar.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(activity.Description))
            {
                AppendTerminal($"[{DateTime.Now:HH:mm:ss}] {activity.Description}\n");
            }
        });
    }

    private Task<(bool Approved, bool Remember)> PromptUserApprovalAsync(CodingPermissionRequest req)
    {
        var tcs = new TaskCompletionSource<(bool Approved, bool Remember)>();
        _pendingApprovalTcs = tcs;

        DispatcherQueue.TryEnqueue(() =>
        {
            ApprovalBar.Title = $"Permission Request: {req.Operation}";
            ApprovalBar.Message = $"Resource: {req.Resource}\n{req.Details}";
            ApprovalBar.IsOpen = true;
        });

        return tcs.Task;
    }

    private void OnApproveOnceClicked(object sender, RoutedEventArgs e)
    {
        ApprovalBar.IsOpen = false;
        _pendingApprovalTcs?.TrySetResult((true, false));
    }

    private void OnApproveAlwaysClicked(object sender, RoutedEventArgs e)
    {
        ApprovalBar.IsOpen = false;
        _pendingApprovalTcs?.TrySetResult((true, true));
    }

    private void OnDenyClicked(object sender, RoutedEventArgs e)
    {
        ApprovalBar.IsOpen = false;
        _pendingApprovalTcs?.TrySetResult((false, false));
    }

    private async void OnTerminalRunClicked(object sender, RoutedEventArgs e)
    {
        var cmd = TerminalInputBox.Text.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        TerminalInputBox.Text = string.Empty;
        AppendTerminal($"$ {cmd}\n");

        var res = await _codingService.CliEngine.ExecuteAsync(cmd);
        AppendTerminal(res.StandardOutput);
        if (!string.IsNullOrEmpty(res.StandardError))
        {
            AppendTerminal("\n[ERROR] " + res.StandardError);
        }
        AppendTerminal($"\n[Process exited with code {res.ExitCode} in {res.WallTimeSeconds:N2}s]\n");
    }

    private void OnTerminalInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            OnTerminalRunClicked(sender, e);
        }
    }

    private void AppendTerminal(string text)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TerminalOutputText.Text += text;
            TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
        });
    }

    private void AddSystemMessage(string content)
    {
        OnMessageAppended(this, new CodingMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Role = "system",
            Content = content,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
