using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using OpenClaw.Shared.Divisions;
using Windows.System;

namespace OpenClawTray.Controls;

public sealed partial class DivisionSwitcher : UserControl
{
    private static readonly SolidColorBrush SelectedTextBrush = new(Colors.White);
    private static readonly SolidColorBrush UnselectedTextBrush = new(ColorHelper.FromArgb(255, 160, 165, 173));

    private readonly Dictionary<SparkDivision, Button> _segmentButtons = new();
    private Storyboard? _currentStoryboard;
    private bool _isLoaded;
    private SparkDivision _selectedDivision = SparkDivision.Chat;

    public event EventHandler<SparkDivision>? DivisionSelected;

    public SparkDivision SelectedDivision
    {
        get => _selectedDivision;
        set => SetSelectedDivision(value, animate: _isLoaded, notify: false);
    }

    public DivisionSwitcher()
    {
        InitializeComponent();

        _segmentButtons[SparkDivision.Chat] = ChatSegment;
        _segmentButtons[SparkDivision.Work] = WorkSegment;
        _segmentButtons[SparkDivision.Code] = CodeSegment;
        _segmentButtons[SparkDivision.Study] = StudySegment;
        _segmentButtons[SparkDivision.Creative] = CreativeSegment;
        _segmentButtons[SparkDivision.VoiceMode] = VoiceModeSegment;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            UpdateIndicatorPosition(animate: false);
            HighlightSelectedText();
        });
    }

    private void OnButtonsPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isLoaded)
        {
            UpdateIndicatorPosition(animate: false);
        }
    }

    public void SetSelectedDivision(SparkDivision division, bool animate = true, bool notify = true)
    {
        if (_selectedDivision == division && _isLoaded)
            return;

        _selectedDivision = division;
        UpdateIndicatorPosition(animate);
        HighlightSelectedText();

        if (notify)
        {
            DivisionSelected?.Invoke(this, division);
        }
    }

    private void OnSegmentClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagStr && Enum.TryParse<SparkDivision>(tagStr, out var division))
        {
            SetSelectedDivision(division, animate: true, notify: true);
        }
    }

    private void OnControlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var currentIndex = (int)_selectedDivision;
        var total = Enum.GetValues<SparkDivision>().Length;

        switch (e.Key)
        {
            case VirtualKey.Left or VirtualKey.Up:
                var prevIndex = (currentIndex - 1 + total) % total;
                SetSelectedDivision((SparkDivision)prevIndex, animate: true, notify: true);
                e.Handled = true;
                break;

            case VirtualKey.Right or VirtualKey.Down:
                var nextIndex = (currentIndex + 1) % total;
                SetSelectedDivision((SparkDivision)nextIndex, animate: true, notify: true);
                e.Handled = true;
                break;

            case VirtualKey.Home:
                SetSelectedDivision(SparkDivision.Chat, animate: true, notify: true);
                e.Handled = true;
                break;

            case VirtualKey.End:
                SetSelectedDivision(SparkDivision.VoiceMode, animate: true, notify: true);
                e.Handled = true;
                break;
        }
    }

    private void HighlightSelectedText()
    {
        var selectedBrush = Resources.TryGetValue("SwitcherTextSelected", out var s) && s is Brush sb ? sb : (Brush)SelectedTextBrush;
        var unselectedBrush = Resources.TryGetValue("SwitcherTextUnselected", out var u) && u is Brush ub ? ub : (Brush)UnselectedTextBrush;

        foreach (var (division, button) in _segmentButtons)
        {
            var isSelected = division == _selectedDivision;
            button.Foreground = isSelected ? selectedBrush : unselectedBrush;
            button.FontWeight = isSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private void UpdateIndicatorPosition(bool animate)
    {
        if (!_segmentButtons.TryGetValue(_selectedDivision, out var targetButton))
            return;

        if (targetButton.ActualWidth <= 0)
        {
            targetButton.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, 26));
        }

        try
        {
            var transform = targetButton.TransformToVisual(ButtonsPanel);
            var point = transform.TransformPoint(new global::Windows.Foundation.Point(0, 0));
            double targetX = point.X;
            double targetWidth = Math.Max(targetButton.ActualWidth, 36);

            _currentStoryboard?.Stop();

            if (!animate || !_isLoaded)
            {
                IndicatorTransform.X = targetX;
                SelectionPill.Width = targetWidth;
                return;
            }

            var storyboard = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var animX = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            };
            Storyboard.SetTarget(animX, IndicatorTransform);
            Storyboard.SetTargetProperty(animX, "X");
            storyboard.Children.Add(animX);

            var animWidth = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease
            };
            Storyboard.SetTarget(animWidth, SelectionPill);
            Storyboard.SetTargetProperty(animWidth, "Width");
            storyboard.Children.Add(animWidth);

            _currentStoryboard = storyboard;
            storyboard.Begin();
        }
        catch
        {
            // Fallback for visual tree transitions before render layout pass
        }
    }
}
