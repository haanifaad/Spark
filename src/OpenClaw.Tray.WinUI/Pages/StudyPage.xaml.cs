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

    public StudyPage()
    {
        InitializeComponent();

        _studyService = new StudyDivisionService();
        _studyService.MessageReceived += OnMessageReceived;
        _studyService.ProgressUpdated += OnProgressUpdated;
        _studyService.ActiveCourseChanged += OnActiveCourseChanged;

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeWorkspaceAsync();
    }

    private async Task InitializeWorkspaceAsync()
    {
        try
        {
            LoadingProgressBar.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = false;

            await _studyService.InitializeAsync();
            var courses = await _studyService.LoadCoursesAsync();
            PopulateCourses(courses);
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
                }
            };

            CoursesPanel.Children.Add(btn);
        }
    }

    private void OnActiveCourseChanged(object? sender, StudyCourse? course)
    {
        if (course == null) return;

        DispatcherQueue.TryEnqueue(() =>
        {
            ActiveCourseBadge.Text = $"Active: {course.Title}";
            EducationLevelBadge.Text = $"Level: {course.Level}";
            ActiveLessonTitle.Text = course.Title;
            ActiveLessonSummary.Text = course.Description;
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

            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 700,
                HorizontalAlignment = message.Sender == StudyMessageSender.User ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = (Brush)Application.Current.Resources[message.Sender == StudyMessageSender.User ? "AccentFillColorDefaultBrush" : "CardBackgroundFillColorSecondaryBrush"]
            };

            var stack = new StackPanel { Spacing = 4 };
            var header = new TextBlock
            {
                Text = message.Sender == StudyMessageSender.User ? "You" : "Study AI Tutor",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources[message.Sender == StudyMessageSender.User ? "TextOnAccentFillColorPrimaryBrush" : "AccentTextFillColorPrimaryBrush"],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            var content = new TextBlock
            {
                Text = message.Content,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources[message.Sender == StudyMessageSender.User ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush"],
                TextWrapping = TextWrapping.Wrap
            };

            stack.Children.Add(header);
            stack.Children.Add(content);
            border.Child = stack;

            MessagesPanel.Children.Add(border);
            MessagesScrollViewer.ChangeView(null, MessagesScrollViewer.ScrollableHeight + 200, null);
        });
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
            var result = await _studyService.SendMessageAsync(text, "default", _activeCts.Token);
            ModelRoutingBadge.Text = $"Model: {result.ModelUsed} ({result.IdentifiedIntent})";
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

            var courseId = _studyService.ActiveCourse?.Id ?? "general";
            await _studyService.StartSessionAsync(courseId);

            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = "New Study Session Initialized";
            StatusInfoBar.Message = "Ready for questions.";
            StatusInfoBar.IsOpen = true;
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

    private void OnPromptSampleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string sample)
        {
            PromptTextBox.Text = sample;
            _ = SendCurrentPromptAsync();
        }
    }
}
