using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Shared.Study.Application;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;

namespace OpenClawTray.Pages;

public sealed partial class StudyPage : Page
{
    private readonly IStudyDivisionService _studyService;
    private CancellationTokenSource? _activeCts;
    private bool _isInitializingControls = true;
    private bool _workspaceInitialized;

    public StudyPage()
    {
        _isInitializingControls = true;

        _studyService = new StudyDivisionService();
        _studyService.MessageReceived += OnMessageReceived;
        _studyService.ProgressUpdated += OnProgressUpdated;
        _studyService.ActiveCourseChanged += OnActiveCourseChanged;
        _studyService.ActiveSessionChanged += OnActiveSessionChanged;

        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (_workspaceInitialized) return;
        _workspaceInitialized = true;
        await InitializeWorkspaceAsync();
    }

    private async Task InitializeWorkspaceAsync()
    {
        try
        {
            LoadingProgressBar.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = false;
            _isInitializingControls = true;

            await _studyService.InitializeAsync();
            var courses = await _studyService.LoadCoursesAsync();
            PopulateCourses(courses);

            var skills = await _studyService.SkillRegistry.ListSkillsAsync(includeDisabled: true);
            SkillsCountBadge.Text = $"Skills: {skills.Count}";
            ToolsCountBadge.Text = $"Tools: {_studyService.ToolRegistry.ListTools().Count}";

            await LoadPastSessionsAsync();
            await LoadActiveObjectivesAsync();
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Study Division Initialization Error";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            _isInitializingControls = false;
            LoadingProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void PopulateCourses(IReadOnlyList<StudyCourse> courses)
    {
        CoursesPanel.Children.Clear();

        if (courses.Count == 0)
        {
            CoursesPanel.Children.Add(new TextBlock
            {
                Text = "No courses enrolled.",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            return;
        }

        foreach (var course in courses)
        {
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 8, 10, 8),
                Tag = course.Id
            };

            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock
            {
                Text = course.Title,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            sp.Children.Add(new TextBlock
            {
                Text = course.Description,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2
            });

            btn.Content = sp;
            btn.Click += async (s, e) =>
            {
                if (s is Button b && b.Tag is string courseId)
                {
                    await _studyService.SetActiveCourseAsync(courseId);
                    await LoadActiveObjectivesAsync();
                }
            };

            CoursesPanel.Children.Add(btn);
        }
    }

    private async Task LoadPastSessionsAsync()
    {
        try
        {
            var sessions = await _studyService.DataStore.ListSessionsAsync("default");
            PastSessionsPanel.Children.Clear();

            if (sessions.Count == 0)
            {
                PastSessionsPanel.Children.Add(new TextBlock
                {
                    Text = "No previous sessions recorded.",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
                return;
            }

            foreach (var session in sessions.TakeLast(5).Reverse())
            {
                var btn = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 6, 8, 6),
                    Tag = session.Id
                };

                var sp = new StackPanel { Spacing = 2 };
                sp.Children.Add(new TextBlock
                {
                    Text = $"{session.Title} [{session.TeachingMode}]",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{session.Messages.Count} turns • {session.StartedAtUtc:MMM dd, HH:mm}",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });

                btn.Content = sp;
                btn.Click += async (s, e) =>
                {
                    if (s is Button b && b.Tag is string sessId)
                    {
                        await ResumeSelectedSessionAsync(sessId);
                    }
                };

                PastSessionsPanel.Children.Add(btn);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private async Task LoadActiveObjectivesAsync()
    {
        try
        {
            ObjectivesListPanel.Children.Clear();
            var lessonId = _studyService.ActiveSession?.LessonId ?? "lesson-asymptotic";
            var objectives = await _studyService.DataStore.ListObjectivesAsync(lessonId, "default");

            if (objectives.Count == 0)
            {
                ObjectivesListPanel.Children.Add(new TextBlock
                {
                    Text = "No specific objectives set for active lesson.",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
                return;
            }

            foreach (var obj in objectives)
            {
                var check = new CheckBox
                {
                    Content = $"[{obj.TaxonomyLevel}] {obj.Description}",
                    IsChecked = obj.IsMastered,
                    Tag = obj.Id,
                    FontSize = 12
                };

                check.Click += async (s, e) =>
                {
                    if (s is CheckBox cb && cb.Tag is string objId)
                    {
                        var isChecked = cb.IsChecked == true;
                        await _studyService.Api.MarkObjectiveMasteredAsync(objId, isChecked, "default");
                    }
                };

                ObjectivesListPanel.Children.Add(check);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private async Task ResumeSelectedSessionAsync(string sessionId)
    {
        try
        {
            LoadingProgressBar.Visibility = Visibility.Visible;
            var session = await _studyService.ResumeSessionAsync(sessionId);

            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = "Session Resumed";
            StatusInfoBar.Message = $"Resumed session '{session.Title}'.";
            StatusInfoBar.IsOpen = true;

            await LoadActiveObjectivesAsync();
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Session Resumption Error";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            LoadingProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void OnActiveCourseChanged(object? sender, StudyCourse? course)
    {
        if (course == null) return;

        var dq = DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(() =>
        {
            if (ActiveCourseBadge == null) return;
            ActiveCourseBadge.Text = $"Active: {course.Title}";
            ActiveLessonTitle.Text = course.Title;
            ActiveLessonSummary.Text = course.Description;
            ActiveTopicIndicator.Text = $"Topic: {course.Title}";
        });
    }

    private void OnActiveSessionChanged(object? sender, StudySession? session)
    {
        if (session == null) return;

        var dq = DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(() =>
        {
            if (ActiveSessionBadge == null) return;
            ActiveSessionBadge.Text = $"Session: {session.Status} [{session.TeachingMode}]";
            ActiveTopicIndicator.Text = $"Topic: {session.CurrentTopic ?? session.Title}";

            // Re-render existing session messages
            MessagesPanel.Children.Clear();
            if (session.Messages.Count > 0)
            {
                EmptyStateCard.Visibility = Visibility.Collapsed;
                MessagesScrollViewer.Visibility = Visibility.Visible;
                foreach (var msg in session.Messages)
                {
                    RenderMessageBubble(msg);
                }
            }
            else
            {
                EmptyStateCard.Visibility = Visibility.Visible;
                MessagesScrollViewer.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void OnProgressUpdated(object? sender, StudyProgress progress)
    {
        var dq = DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(() =>
        {
            if (MasteryBadge == null) return;
            MasteryBadge.Text = $"Mastery: {progress.MasteryTier}";
            CompletedLessonsText.Text = $"{progress.CompletedLessonIds.Count} lessons completed";
        });
    }

    private void OnMessageReceived(object? sender, StudySessionMessage message)
    {
        var dq = DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(() =>
        {
            if (EmptyStateCard == null) return;
            EmptyStateCard.Visibility = Visibility.Collapsed;
            MessagesScrollViewer.Visibility = Visibility.Visible;
            RenderMessageBubble(message);
        });
    }

    private void RenderMessageBubble(StudySessionMessage message)
    {
        var isUser = message.Sender == StudyMessageSender.User;

        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            MaxWidth = 720,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = (Brush)Application.Current.Resources[isUser ? "AccentFillColorDefaultBrush" : "CardBackgroundFillColorSecondaryBrush"]
        };

        var stack = new StackPanel { Spacing = 4 };

        // Header with sender name and mode/language badge
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headerPanel.Children.Add(new TextBlock
        {
            Text = isUser ? "You" : "SPARK AI Tutor",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources[isUser ? "TextOnAccentFillColorPrimaryBrush" : "AccentTextFillColorPrimaryBrush"],
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        if (!isUser)
        {
            var badgeText = $"{message.TeachingMode} • {message.Language}";
            headerPanel.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Child = new TextBlock
                {
                    Text = badgeText,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontSize = 10
                }
            });
        }

        var content = new TextBlock
        {
            Text = message.Content,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources[isUser ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };

        stack.Children.Add(headerPanel);
        stack.Children.Add(content);
        border.Child = stack;

        MessagesPanel.Children.Add(border);
        MessagesScrollViewer.ChangeView(null, MessagesScrollViewer.ScrollableHeight + 200, null);
    }

    private async void OnSendClicked(object sender, RoutedEventArgs e)
    {
        await SendCurrentPromptAsync();
    }

    private async void OnPromptKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await SendCurrentPromptAsync();
        }
    }

    private async Task SendCurrentPromptAsync()
    {
        var text = PromptTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        AutocompleteBorder.Visibility = Visibility.Collapsed;
        PromptTextBox.Text = string.Empty;
        SendButton.IsEnabled = false;
        LoadingProgressBar.Visibility = Visibility.Visible;

        try
        {
            _activeCts = new CancellationTokenSource();
            var mode = GetSelectedTeachingMode();
            var lang = GetSelectedLanguage();

            var result = await _studyService.ExecutePipelineAsync(text, mode, lang, "default", _activeCts.Token);
            var skillDisplay = result.SelectedSkill != null ? result.SelectedSkill.Name : "Standard";
            ModelRoutingBadge.Text = $"Pipeline: {result.InputType} • {skillDisplay} ({result.TeachingMode})";

            if (result.RequiresApproval && result.PendingApproval != null)
            {
                var toolId = result.SelectedTools.FirstOrDefault() ?? "Tool";
                var approvalId = result.PendingApproval.ApprovalId;
                ConfirmationToolTitle.Text = $"Tool: {toolId}";
                ConfirmationToolDescription.Text = $"Action: Execute {toolId}";
                ConfirmationReason.Text = $"Reason: {result.PendingApproval.DecisionNote ?? "Requires manual authorization."}";
                ConfirmationDialog.XamlRoot = this.XamlRoot;
                var dialogResult = await ConfirmationDialog.ShowAsync();

                if (dialogResult == ContentDialogResult.Primary)
                {
                    await _studyService.ToolRegistry.ApproveAndExecutePendingAsync(
                        approvalId,
                        "Learner",
                        "default",
                        _activeCts.Token);

                    StatusInfoBar.Severity = InfoBarSeverity.Success;
                    StatusInfoBar.Title = "Tool Authorization Granted";
                    StatusInfoBar.Message = $"Authorized and executed tool '{toolId}'.";
                    StatusInfoBar.IsOpen = true;
                }
                else
                {
                    await _studyService.ToolRegistry.RejectPendingAsync(
                        approvalId,
                        "User declined approval in UI dialog.",
                        "Learner",
                        "default",
                        _activeCts.Token);

                    StatusInfoBar.Severity = InfoBarSeverity.Warning;
                    StatusInfoBar.Title = "Tool Authorization Denied";
                    StatusInfoBar.Message = $"Execution of '{toolId}' was denied.";
                    StatusInfoBar.IsOpen = true;
                }
            }
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Message Processing Error";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            SendButton.IsEnabled = true;
            LoadingProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnNewSessionClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            MessagesPanel.Children.Clear();
            EmptyStateCard.Visibility = Visibility.Visible;
            MessagesScrollViewer.Visibility = Visibility.Collapsed;

            var courseId = _studyService.ActiveCourse?.Id ?? "course-foundation-cs";
            var mode = GetSelectedTeachingMode();
            var lang = GetSelectedLanguage();
            var level = GetSelectedLevel();

            await _studyService.StartSessionAsync(courseId, "lesson-asymptotic", mode, lang, level, "Algorithms", "default");

            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = "New Study Session Initialized";
            StatusInfoBar.Message = $"Active mode: {mode}, Language: {lang}";
            StatusInfoBar.IsOpen = true;

            await LoadPastSessionsAsync();
            await LoadActiveObjectivesAsync();
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Session Creation Error";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await InitializeWorkspaceAsync();
    }

    private void OnToggleSessionsDrawerClicked(object sender, RoutedEventArgs e)
    {
        PastSessionsPanel.Visibility = PastSessionsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        PastSessionsHeader.Visibility = PastSessionsPanel.Visibility;
    }

    private async void OnLevelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingControls) return;
        var level = GetSelectedLevel();
        if (_studyService.ActiveSession != null)
        {
            await _studyService.UpdateActiveSessionPreferencesAsync(null, null, level, null);
        }
    }

    private async void OnTeachingModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingControls) return;
        var mode = GetSelectedTeachingMode();
        if (_studyService.ActiveSession != null)
        {
            await _studyService.UpdateActiveSessionPreferencesAsync(mode, null, null, null);
        }
    }

    private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingControls) return;
        var lang = GetSelectedLanguage();
        if (_studyService.ActiveSession != null)
        {
            await _studyService.UpdateActiveSessionPreferencesAsync(null, lang, null, null);
        }
    }

    private TeachingMode GetSelectedTeachingMode()
    {
        if (TeachingModeComboBox.SelectedItem is ComboBoxItem item && item.Content is string text)
        {
            if (Enum.TryParse<TeachingMode>(text, out var mode))
            {
                return mode;
            }
        }
        return TeachingMode.Standard;
    }

    private LearningLanguage GetSelectedLanguage()
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Content is string text)
        {
            if (text.Contains("Malayalam"))
            {
                return LearningLanguage.Malayalam;
            }
        }
        return LearningLanguage.English;
    }

    private EducationLevel GetSelectedLevel()
    {
        if (LevelComboBox.SelectedItem is ComboBoxItem item && item.Content is string text)
        {
            if (Enum.TryParse<EducationLevel>(text, out var level))
            {
                return level;
            }
        }
        return EducationLevel.University;
    }

    private void OnPromptSampleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string sample)
        {
            PromptTextBox.Text = sample;
            _ = SendCurrentPromptAsync();
        }
    }

    private void OnSampleCommandClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sampleCmd)
        {
            PromptTextBox.Text = sampleCmd;
            _ = SendCurrentPromptAsync();
        }
    }

    private void OnPromptTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || AutocompleteSuggestionsPanel == null) return;
        var text = PromptTextBox?.Text?.TrimStart() ?? string.Empty;
        if (text.StartsWith("/"))
        {
            var suggestions = _studyService.CommandRegistry.GetAutocompleteSuggestions(text);
            AutocompleteSuggestionsPanel.Children.Clear();

            if (suggestions.Count > 0)
            {
                foreach (var sug in suggestions.Take(6))
                {
                    var btn = new Button
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(10, 6, 10, 6),
                        Tag = sug.CommandName
                    };

                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                    sp.Children.Add(new TextBlock
                    {
                        Text = $"/{sug.CommandName}",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                        MinWidth = 100
                    });
                    sp.Children.Add(new TextBlock
                    {
                        Text = sug.Description,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 420
                    });

                    btn.Content = sp;
                    btn.Click += (s, args) =>
                    {
                        if (s is Button b && b.Tag is string cmd && PromptTextBox != null)
                        {
                            PromptTextBox.Text = $"/{cmd} ";
                            PromptTextBox.SelectionStart = PromptTextBox.Text.Length;
                            if (AutocompleteBorder != null) AutocompleteBorder.Visibility = Visibility.Collapsed;
                            PromptTextBox.Focus(FocusState.Programmatic);
                        }
                    };

                    AutocompleteSuggestionsPanel.Children.Add(btn);
                }

                AutocompleteBorder.Visibility = Visibility.Visible;
            }
            else
            {
                AutocompleteBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            AutocompleteBorder.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnSkillsExplorerClicked(object sender, RoutedEventArgs e)
    {
        await PopulateSkillsListAsync();
        SkillsExplorerDialog.XamlRoot = this.XamlRoot;
        await SkillsExplorerDialog.ShowAsync();
    }

    private void OnSkillSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || SkillsListDisplayPanel == null) return;
        _ = PopulateSkillsListAsync();
    }

    private void OnSkillCategoryFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || SkillsListDisplayPanel == null) return;
        _ = PopulateSkillsListAsync();
    }

    private async Task PopulateSkillsListAsync()
    {
        if (SkillsListDisplayPanel == null || _studyService == null) return;
        SkillsListDisplayPanel.Children.Clear();
        var search = SkillSearchTextBox.Text?.Trim() ?? string.Empty;
        var selectedItem = SkillCategoryComboBox.SelectedItem as ComboBoxItem;
        var catName = selectedItem?.Content as string;

        SkillCategory? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(catName) && catName != "All Categories")
        {
            if (Enum.TryParse<SkillCategory>(catName, out var parsedCat))
            {
                categoryFilter = parsedCat;
            }
        }

        var skills = string.IsNullOrWhiteSpace(search)
            ? await _studyService.SkillRegistry.ListSkillsAsync(categoryFilter, includeDisabled: true)
            : await _studyService.SkillRegistry.SearchSkillsAsync(search);

        if (categoryFilter.HasValue && !string.IsNullOrWhiteSpace(search))
        {
            skills = skills.Where(s => s.Category == categoryFilter.Value).ToList();
        }

        if (skills.Count == 0)
        {
            SkillsListDisplayPanel.Children.Add(new TextBlock
            {
                Text = "No skills found matching filter criteria.",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            return;
        }

        foreach (var skill in skills)
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel { Spacing = 3 };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            titleRow.Children.Add(new TextBlock
            {
                Text = skill.Name,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            });
            titleRow.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Child = new TextBlock
                {
                    Text = skill.Category.ToString(),
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
                    FontSize = 10
                }
            });

            infoStack.Children.Add(titleRow);
            infoStack.Children.Add(new TextBlock
            {
                Text = skill.Description,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = $"Allowed Tools: {skill.AllowedTools.Count} • Mode: {skill.ModelPreference ?? "Default"}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                FontSize = 11
            });

            var toggle = new ToggleSwitch
            {
                IsOn = skill.Status == SkillStatus.Active,
                OnContent = "Active",
                OffContent = "Disabled",
                Tag = skill.Id,
                VerticalAlignment = VerticalAlignment.Center
            };
            toggle.Toggled += async (s, e) =>
            {
                if (s is ToggleSwitch ts && ts.Tag is string sId)
                {
                    await _studyService.SkillRegistry.SetSkillEnabledAsync(sId, ts.IsOn, "default");
                }
            };

            Grid.SetColumn(infoStack, 0);
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(infoStack);
            grid.Children.Add(toggle);

            card.Child = grid;
            SkillsListDisplayPanel.Children.Add(card);
        }
    }

    private async void OnToolsExplorerClicked(object sender, RoutedEventArgs e)
    {
        PopulateToolsList();
        ToolsExplorerDialog.XamlRoot = this.XamlRoot;
        await ToolsExplorerDialog.ShowAsync();
    }

    private void OnToolSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || ToolsListDisplayPanel == null) return;
        PopulateToolsList();
    }

    private void OnToolCategoryFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || ToolsListDisplayPanel == null) return;
        PopulateToolsList();
    }

    private void PopulateToolsList()
    {
        if (ToolsListDisplayPanel == null || _studyService == null) return;
        ToolsListDisplayPanel.Children.Clear();
        var search = ToolSearchTextBox.Text?.Trim() ?? string.Empty;
        var selectedItem = ToolCategoryComboBox.SelectedItem as ComboBoxItem;
        var catName = selectedItem?.Content as string;

        ToolCategory? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(catName) && catName != "All Categories")
        {
            if (Enum.TryParse<ToolCategory>(catName, out var parsedCat))
            {
                categoryFilter = parsedCat;
            }
        }

        var tools = string.IsNullOrWhiteSpace(search)
            ? _studyService.ToolRegistry.ListTools(categoryFilter)
            : _studyService.ToolRegistry.SearchTools(search);

        if (categoryFilter.HasValue && !string.IsNullOrWhiteSpace(search))
        {
            tools = tools.Where(t => t.Category == categoryFilter.Value).ToList();
        }

        if (tools.Count == 0)
        {
            ToolsListDisplayPanel.Children.Add(new TextBlock
            {
                Text = "No tools found matching filter criteria.",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            return;
        }

        foreach (var tool in tools)
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var stack = new StackPanel { Spacing = 4 };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock
            {
                Text = tool.Name,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            });
            header.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Child = new TextBlock
                {
                    Text = tool.Category.ToString(),
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontSize = 10
                }
            });

            var requiresApproval = tool.RequiredPermission >= StudyPermissionKind.FileModification;
            if (requiresApproval)
            {
                header.Children.Add(new Border
                {
                    Background = (Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"],
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 1, 6, 1),
                    Child = new TextBlock
                    {
                        Text = "Approval Required",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    }
                });
            }

            stack.Children.Add(header);
            stack.Children.Add(new TextBlock
            {
                Text = tool.Description,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"ID: {tool.Id} • Timeout: {tool.TimeoutMs / 1000}s • Permission: {tool.RequiredPermission}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                FontSize = 11
            });

            card.Child = stack;
            ToolsListDisplayPanel.Children.Add(card);
        }
    }

    private async void OnExecutionHistoryClicked(object sender, RoutedEventArgs e)
    {
        await PopulateHistoryListAsync();
        ExecutionHistoryDialog.XamlRoot = this.XamlRoot;
        await ExecutionHistoryDialog.ShowAsync();
    }

    private void OnHistoryTypeFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || HistoryListDisplayPanel == null) return;
        _ = PopulateHistoryListAsync();
    }

    private async Task PopulateHistoryListAsync()
    {
        if (HistoryListDisplayPanel == null || _studyService == null) return;
        HistoryListDisplayPanel.Children.Clear();
        var selectedItem = HistoryTypeComboBox.SelectedItem as ComboBoxItem;
        var type = selectedItem?.Content as string ?? "Tool Executions";

        if (type == "Tool Executions")
        {
            var history = await _studyService.ToolRegistry.GetToolExecutionHistoryAsync(null, "default");
            if (history.Count == 0)
            {
                HistoryListDisplayPanel.Children.Add(new TextBlock
                {
                    Text = "No tool executions recorded yet.",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
                return;
            }

            foreach (var rec in history.Take(25))
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6)
                };
                var sp = new StackPanel { Spacing = 2 };
                sp.Children.Add(new TextBlock
                {
                    Text = $"{rec.ToolId} [{rec.Status}]",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{rec.TimestampUtc:HH:mm:ss} • {rec.DurationMs}ms • Output: {(rec.Success ? "Success" : rec.ErrorMessage)}",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                card.Child = sp;
                HistoryListDisplayPanel.Children.Add(card);
            }
        }
        else if (type == "Skill Executions")
        {
            var history = await _studyService.SkillRegistry.GetSkillExecutionHistoryAsync(null, "default");
            if (history.Count == 0)
            {
                HistoryListDisplayPanel.Children.Add(new TextBlock
                {
                    Text = "No skill executions recorded yet.",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
                return;
            }

            foreach (var rec in history.Take(25))
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6)
                };
                var sp = new StackPanel { Spacing = 2 };
                sp.Children.Add(new TextBlock
                {
                    Text = $"{rec.SkillId} [{rec.Status}]",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{rec.StartedAtUtc:HH:mm:ss} • {rec.DurationMs}ms • Status: {rec.Status}",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
                card.Child = sp;
                HistoryListDisplayPanel.Children.Add(card);
            }
        }
        else
        {
            var history = await _studyService.CommandRegistry.GetCommandExecutionHistoryAsync(null, "default");
            if (history.Count == 0)
            {
                HistoryListDisplayPanel.Children.Add(new TextBlock
                {
                    Text = "No command executions recorded yet.",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
                return;
            }

            foreach (var rec in history.Take(25))
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6)
                };
                var sp = new StackPanel { Spacing = 2 };
                sp.Children.Add(new TextBlock
                {
                    Text = $"/{rec.CommandName} [{rec.Status}]",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{rec.ExecutedAtUtc:HH:mm:ss} • {rec.RawCommand}",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                card.Child = sp;
                HistoryListDisplayPanel.Children.Add(card);
            }
        }
    }

    private async void OnCommandHelpClicked(object sender, RoutedEventArgs e)
    {
        PopulateCommandHelpList();
        CommandHelpDialog.XamlRoot = this.XamlRoot;
        await CommandHelpDialog.ShowAsync();
    }

    private void PopulateCommandHelpList()
    {
        CommandHelpListPanel.Children.Clear();
        var commands = _studyService.CommandRegistry.ListCommands();

        foreach (var cmd in commands.OrderBy(c => c.Category).ThenBy(c => c.Name))
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var stack = new StackPanel { Spacing = 4 };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock
            {
                Text = $"/{cmd.Name}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            });
            header.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Child = new TextBlock
                {
                    Text = cmd.Category.ToString(),
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontSize = 10
                }
            });

            stack.Children.Add(header);
            stack.Children.Add(new TextBlock
            {
                Text = cmd.Description,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"Syntax: {cmd.UsageExample}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            });

            if (cmd.Aliases.Count > 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"Aliases: {string.Join(", ", cmd.Aliases.Select(a => "/" + a))}",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                    FontSize = 10
                });
            }

            card.Child = stack;
            CommandHelpListPanel.Children.Add(card);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Phase 4: Knowledge Library, Grounded Search & Research UI
    // ══════════════════════════════════════════════════════════════════

    private async void OnKnowledgeLibraryClicked(object sender, RoutedEventArgs e)
    {
        await RefreshKnowledgeDisplayAsync();
        KnowledgeLibraryDialog.XamlRoot = this.XamlRoot;
        await KnowledgeLibraryDialog.ShowAsync();
    }

    private async void OnKnowledgeFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || KnowledgeListDisplayPanel == null) return;
        await RefreshKnowledgeDisplayAsync(KnowledgeFilterTextBox?.Text);
    }

    private async void OnIngestSampleDocClicked(object sender, RoutedEventArgs e)
    {
        var sampleContent = @"# Algorithms & Complexity Primer
Asymptotic notation describes the limiting behavior of execution time as input size n tends to infinity. Big O represents an upper bound on worst-case computational complexity.

## Divide and Conquer
Divide and conquer algorithms partition a problem into smaller subproblems, solve each recursively, and recombine solutions. MergeSort achieves guaranteed O(n log n) runtime, whereas QuickSort achieves O(n log n) expected runtime with in-place partitioning.";

        await _studyService.Ingestion.IngestContentAsync("Algorithms-And-Complexity-Primer.md", ".md", sampleContent, "default");
        await RefreshKnowledgeDisplayAsync();
    }

    private async Task RefreshKnowledgeDisplayAsync(string? filter = null)
    {
        if (KnowledgeListDisplayPanel == null || _studyService == null) return;
        KnowledgeListDisplayPanel.Children.Clear();
        var docs = await _studyService.Knowledge.ListDocumentsAsync();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var term = filter.Trim();
            docs = docs.Where(d => d.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                   d.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (docs.Count == 0)
        {
            KnowledgeListDisplayPanel.Children.Add(new TextBlock
            {
                Text = "No documents found in knowledge repository.",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            return;
        }

        foreach (var doc in docs)
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var stack = new StackPanel { Spacing = 4 };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = doc.Title,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            };
            Grid.SetColumn(titleBlock, 0);
            header.Children.Add(titleBlock);

            var statusBadge = new Border
            {
                Background = doc.Status == DocumentProcessingStatus.Indexed
                    ? (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"]
                    : (Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"],
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = doc.Status.ToString(),
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontSize = 10
                }
            };
            Grid.SetColumn(statusBadge, 1);
            header.Children.Add(statusBadge);

            stack.Children.Add(header);
            stack.Children.Add(new TextBlock
            {
                Text = $"File: {doc.FileName} • Format: {doc.Format} • Pages: {doc.PageCount} • Size: {doc.FileSizeBytes:N0} bytes",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            card.Child = stack;
            KnowledgeListDisplayPanel.Children.Add(card);
        }
    }

    private async void OnGroundedSearchClicked(object sender, RoutedEventArgs e)
    {
        GroundedSearchDialog.XamlRoot = this.XamlRoot;
        await GroundedSearchDialog.ShowAsync();
    }

    private void OnGroundedSearchQueryChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializingControls || _studyService == null || SearchResultsDisplayPanel == null) return;
        // Ready for search execution
    }

    private async void OnGroundedSearchExecuteClicked(object sender, RoutedEventArgs e)
    {
        if (_studyService == null || SearchResultsDisplayPanel == null || GroundedSearchTextBox == null) return;
        var query = GroundedSearchTextBox.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        SearchResultsDisplayPanel.Children.Clear();
        var searchResponse = await _studyService.Search.SearchAsync(new StudySearchRequest(query, "default"));

        if (searchResponse.Results.Count == 0)
        {
            SearchResultsDisplayPanel.Children.Add(new TextBlock
            {
                Text = $"No grounded matches found for '{query}'.",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            return;
        }

        foreach (var item in searchResponse.Results)
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(new TextBlock
            {
                Text = $"{item.DocumentTitle} (Page {item.PageNumber}) — Score: {item.RelevanceScore:F2}",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.Snippet,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            });

            card.Child = stack;
            SearchResultsDisplayPanel.Children.Add(card);
        }
    }

    private async void OnResearchWorkspaceClicked(object sender, RoutedEventArgs e)
    {
        if (_studyService == null || ResearchProjectsDisplayPanel == null) return;
        ResearchProjectsDisplayPanel.Children.Clear();
        var projects = await _studyService.Research.ListProjectsAsync();

        if (projects.Count == 0)
        {
            ResearchProjectsDisplayPanel.Children.Add(new TextBlock
            {
                Text = "No active research projects found. Create one via /research topic=\"<Your Topic>\".",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }
        else
        {
            foreach (var proj in projects)
            {
                var card = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 10, 14, 10)
                };

                var stack = new StackPanel { Spacing = 6 };
                stack.Children.Add(new TextBlock
                {
                    Text = proj.Title,
                    Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                    FontSize = 14
                });

                if (!string.IsNullOrWhiteSpace(proj.Hypothesis))
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"Hypothesis: {proj.Hypothesis}",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                    });
                }

                stack.Children.Add(new TextBlock
                {
                    Text = proj.Description,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });

                var questions = await _studyService.Research.ListQuestionsAsync(proj.Id);
                var evidence = await _studyService.Research.ListEvidenceAsync(proj.Id);
                var citations = await _studyService.Research.ListCitationsAsync(proj.Id);

                stack.Children.Add(new TextBlock
                {
                    Text = $"Inquiries: {questions.Count} • Citations: {citations.Count} • Evidence Records: {evidence.Count}",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                    FontSize = 11
                });

                card.Child = stack;
                ResearchProjectsDisplayPanel.Children.Add(card);
            }
        }

        ResearchWorkspaceDialog.XamlRoot = this.XamlRoot;
        await ResearchWorkspaceDialog.ShowAsync();
    }
}
