using System;
using Windows.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Transport toolbar for the timeline: play/pause, step to previous/next event, and the threads and
/// audio toggles. Raises an event per control; the owner performs the action.
/// </summary>
internal sealed class TimelineTransport : StackPanel, IDisposable
{
    private const double ButtonHeight = 26;

    private const string PlayGlyph = "";
    private const string PauseGlyph = "";
    private const string AudioOnGlyph = "";
    private const string AudioOffGlyph = "";

    private readonly Button _playButton;
    private readonly Button _stepBackButton;
    private readonly Button _stepForwardButton;
    private readonly ToggleButton _threadsButton;
    private readonly ToggleButton _audioButton;

    public event Action? PlayPauseRequested;

    /// <summary>Raised on a step; the argument is true for forward, false for back.</summary>
    public event Action<bool>? StepRequested;

    public event Action<bool>? ThreadsToggled;

    public event Action<bool>? AudioToggled;

    public TimelineTransport()
    {
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Left;

        _playButton = new Button
        {
            Content = new FontIcon { Glyph = PlayGlyph, FontSize = 14 },
            Width = 36,
            Height = ButtonHeight,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
        };
        _playButton.Click += OnPlayButtonClick;

        // Skip-to-previous / skip-to-next glyphs (Segoe Fluent Icons).
        _stepBackButton = MakeButton(new FontIcon { Glyph = "", FontSize = 12 }, 30);
        _stepBackButton.Click += OnStepBackButtonClick;

        _stepForwardButton = MakeButton(new FontIcon { Glyph = "", FontSize = 12 }, 30);
        _stepForwardButton.Click += OnStepForwardButtonClick;

        _threadsButton = new ToggleButton
        {
            Content = new TextBlock { Text = "Threads", FontSize = 10 },
            Height = ButtonHeight,
            Margin = new Thickness(8, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = null,
            Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
        };
        _threadsButton.Checked += OnThreadsToggled;
        _threadsButton.Unchecked += OnThreadsToggled;

        _audioButton = new ToggleButton
        {
            Content = new FontIcon { Glyph = AudioOffGlyph, FontSize = 12 },
            Height = ButtonHeight,
            Width = 34,
            Margin = new Thickness(8, 2, 0, 2),
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = null,
            Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
        };
        ToolTipService.SetToolTip(_audioButton, "Toggle audio feedback");
        _audioButton.Checked += OnAudioToggled;
        _audioButton.Unchecked += OnAudioToggled;

        Children.Add(_stepBackButton);
        Children.Add(_playButton);
        Children.Add(_stepForwardButton);
        Children.Add(_threadsButton);
        Children.Add(_audioButton);
    }

    /// <summary>Swaps the play/pause glyph to reflect the current playback state.</summary>
    public void SetPlaying(bool isPlaying)
    {
        if (_playButton.Content is FontIcon icon)
        {
            icon.Glyph = isPlaying ? PauseGlyph : PlayGlyph;
        }
    }

    private static Button MakeButton(FrameworkElement content, double width) => new()
    {
        Content = content,
        Width = width,
        Height = ButtonHeight,
        Padding = new Thickness(0),
        Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(0),
    };

    private void OnPlayButtonClick(object sender, RoutedEventArgs e) => PlayPauseRequested?.Invoke();

    private void OnStepBackButtonClick(object sender, RoutedEventArgs e) => StepRequested?.Invoke(false);

    private void OnStepForwardButtonClick(object sender, RoutedEventArgs e) => StepRequested?.Invoke(true);

    private void OnThreadsToggled(object sender, RoutedEventArgs e) => ThreadsToggled?.Invoke(_threadsButton.IsChecked == true);

    private void OnAudioToggled(object sender, RoutedEventArgs e)
    {
        var enabled = _audioButton.IsChecked == true;

        if (_audioButton.Content is FontIcon icon)
        {
            icon.Glyph = enabled ? AudioOnGlyph : AudioOffGlyph;
        }

        AudioToggled?.Invoke(enabled);
    }

    public void Dispose()
    {
        _playButton.Click -= OnPlayButtonClick;
        _stepBackButton.Click -= OnStepBackButtonClick;
        _stepForwardButton.Click -= OnStepForwardButtonClick;
        _threadsButton.Checked -= OnThreadsToggled;
        _threadsButton.Unchecked -= OnThreadsToggled;
        _audioButton.Checked -= OnAudioToggled;
        _audioButton.Unchecked -= OnAudioToggled;
    }
}
