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
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

        _studyService = new StudyDivisionService();
        _studyService.MessageReceived += OnMessageReceived;
        _studyService.ProgressUpdated += OnProgressUpdated;
        _studyService.ActiveCourseChanged += OnActiveCourseChanged;
        _studyService.ActiveSessionChanged += OnActiveSessionChanged;

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

        DispatcherQueue.TryEnqueue(() =>
        {
            ActiveCourseBadge.Text = $"Active: {course.Title}";
            ActiveLessonTitle.Text = course.Title;
            ActiveLessonSummary.Text = course.Description;
            ActiveTopicIndicator.Text = $"Topic: {course.Title}";
        });
    }

    private void OnActiveSessionChanged(object? sender, StudySession? session)
    {
        if (session == null) return;

        DispatcherQueue.TryEnqueue(() =>
        {
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
        DispatcherQueue.TryEnqueue(() =>
        {
            MasteryBadge.Text = $"Mastery: {progress.MasteryTier}";
            CompletedLessonsText.Text = $"{progress.CompletedLessonIds.Count} lessons completed";
        });
    }

    private void OnMessageReceived(object? sender, StudySessionMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
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

        PromptTextBox.Text = string.Empty;
        SendButton.IsEnabled = false;
        LoadingProgressBar.Visibility = Visibility.Visible;

        try
        {
            _activeCts = new CancellationTokenSource();
            var mode = GetSelectedTeachingMode();
            var lang = GetSelectedLanguage();

            var resultMsg = await _studyService.SendLearnerMessageAsync(text, mode, lang, "default", _activeCts.Token);
            ModelRoutingBadge.Text = $"Model: {resultMsg.ModelUsed ?? "spark-tutor"} ({resultMsg.TeachingMode})";
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
}
