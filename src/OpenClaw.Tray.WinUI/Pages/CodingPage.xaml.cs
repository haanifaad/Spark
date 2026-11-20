using System;
using System.Collections.Generic;
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
using OpenClaw.Shared.Coding.Models;
using OpenClaw.Shared.Coding.Permissions;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OpenClawTray.Pages;

public sealed partial class CodingPage : Page
{
    private static App CurrentApp => (App)Microsoft.UI.Xaml.Application.Current!;
    private readonly ICodingDivisionService _codingService;
    private TaskCompletionSource<(bool Approved, bool Remember)>? _pendingApprovalTcs;
    private FileChangeRecord? _selectedChange;
    private CancellationTokenSource? _activeTaskCts;

    public CodingPage()
    {
        InitializeComponent();

        _codingService = new CodingDivisionService();
        _codingService.ActivityChanged += OnActivityChanged;
        _codingService.PlanUpdated += OnPlanUpdated;
        _codingService.FileChanged += OnFileChanged;
        _codingService.MessageAppended += OnMessageAppended;

        if (_codingService is CodingDivisionService concreteService)
        {
            concreteService.PermissionManager.UserPromptHandler = PromptUserApprovalAsync;
        }

        // Initialize with default workspace if available
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var defaultPath = @"C:\Intel\Spark\Spark-main";
        if (Directory.Exists(defaultPath))
        {
            await OpenWorkspaceInternalAsync(defaultPath);
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
            // Fallback for unpackaged or dialog failure
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
            ReviewScoreText.Text = $"Quality Score: {report.QualityScore:N0} / 100";

            ReviewFindingsPanel.Children.Clear();
            foreach (var finding in report.Findings)
            {
                var border = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
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
                        : new SolidColorBrush(Colors.Goldenrod)
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

    private async void OnSendClicked(object sender, RoutedEventArgs e)
    {
        var prompt = PromptInputBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        PromptInputBox.Text = string.Empty;
        SendButton.IsEnabled = false;
        ActivityProgressBar.Visibility = Visibility.Visible;

        _activeTaskCts = new CancellationTokenSource();

        try
        {
            await _codingService.SendPromptAsync(prompt, ct: _activeTaskCts.Token);
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
        DiffViewHost.Visibility = TabDiff.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TerminalViewHost.Visibility = TabTerminal.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ReviewViewHost.Visibility = TabReview.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

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
                        icon.Foreground = new SolidColorBrush(Colors.LightGreen);
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
                Foreground = new SolidColorBrush(Colors.LightGreen)
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

        var res = await _codingService.ExecuteCommandAsync(cmd);
        AppendTerminal(res.CombinedOutput + $"\n[Process exited with code {res.ExitCode}]\n");
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
