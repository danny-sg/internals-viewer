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

    private static readonly double[] PlaySpeeds = [0.5, 1, 5, 10];

    private readonly Button _playButton;
    private readonly Button _stepBackButton;
    private readonly Button _stepForwardButton;
    private readonly Button _playSpeedButton;
    private readonly ToggleButton _threadsButton;
    private readonly ToggleButton _audioButton;
    private readonly ProgressRing _audioProgress;

    private int _playSpeedIndex = 1;

    public event Action? PlayPauseRequested;

    /// <summary>Raised on a step; the argument is true for forward, false for back.</summary>
    public event Action<bool>? StepRequested;

    public event Action<double>? PlaySpeedChanged;

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

        _playSpeedButton = MakeButton(new TextBlock
        {
            Text = FormatSpeed(PlaySpeeds[_playSpeedIndex]),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        }, 34);
        ToolTipService.SetToolTip(_playSpeedButton, "Playback speed");
        _playSpeedButton.Click += OnPlaySpeedButtonClick;

        _threadsButton = new ToggleButton
        {
            Content = new TextBlock { Text = "Threads", FontSize = 10 },
            Height = ButtonHeight,
            Margin = new Thickness(2, 2, 0, 2),
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

        _audioProgress = new ProgressRing
        {
            Width = 14,
            Height = 14,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,

            // Matches the button's own margin so the ring centres on the button face rather than on the
            // slot, which includes the 8px gutter to the left of it.
            Margin = new Thickness(8, 2, 0, 2),
        };

        // The ring sits over the audio button rather than beside it, so the transport doesn't reflow
        // (and shift the buttons under the pointer) when a build starts.
        var audioSlot = new Grid();
        audioSlot.Children.Add(_audioButton);
        audioSlot.Children.Add(_audioProgress);

        Children.Add(_stepBackButton);
        Children.Add(_playButton);
        Children.Add(_stepForwardButton);
        Children.Add(_playSpeedButton);
        Children.Add(_threadsButton);
        Children.Add(audioSlot);
    }

    /// <summary>
    /// Shows a spinner over the audio toggle while the audio player builds its voices
    /// </summary>
    /// <remarks>
    /// The toggle is disabled for the duration; the build isn't cancellable, so letting it be switched back off would
    /// only desync the glyph from what the player is actually doing.
    /// </remarks>
    public void SetAudioLoading(bool isLoading)
    {
        _audioProgress.IsActive = isLoading;
        _audioProgress.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

        _audioButton.IsEnabled = !isLoading;

        if (_audioButton.Content is FontIcon icon)
        {
            icon.Opacity = isLoading ? 0 : 1;
        }
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

    private static string FormatSpeed(double speed) => $"{speed:0.#}x";

    private void OnPlayButtonClick(object sender, RoutedEventArgs e) => PlayPauseRequested?.Invoke();

    private void OnStepBackButtonClick(object sender, RoutedEventArgs e) => StepRequested?.Invoke(false);

    private void OnStepForwardButtonClick(object sender, RoutedEventArgs e) => StepRequested?.Invoke(true);

    private void OnPlaySpeedButtonClick(object sender, RoutedEventArgs e)
    {
        _playSpeedIndex = (_playSpeedIndex + 1) % PlaySpeeds.Length;

        if (_playSpeedButton.Content is TextBlock text)
        {
            text.Text = FormatSpeed(PlaySpeeds[_playSpeedIndex]);
        }

        PlaySpeedChanged?.Invoke(PlaySpeeds[_playSpeedIndex]);
    }

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
        _playSpeedButton.Click -= OnPlaySpeedButtonClick;
        _threadsButton.Checked -= OnThreadsToggled;
        _threadsButton.Unchecked -= OnThreadsToggled;
        _audioButton.Checked -= OnAudioToggled;
        _audioButton.Unchecked -= OnAudioToggled;
    }
}
