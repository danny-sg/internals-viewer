using System;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query.Trace.CallStack;

public sealed partial class CallSearchBar : UserControl
{
    private const int SearchDelayMilliseconds = 300;

    public static readonly DependencyProperty CommandsProperty =
        DependencyProperty.Register(nameof(Commands), typeof(object), typeof(CallSearchBar), new PropertyMetadata(null));

    public object? Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    public static readonly DependencyProperty MatchCountProperty =
        DependencyProperty.Register(nameof(MatchCount), typeof(int?), typeof(CallSearchBar), new PropertyMetadata(null));

    public int? MatchCount
    {
        get => (int?)GetValue(MatchCountProperty);
        set => SetValue(MatchCountProperty, value);
    }

    public CallSearchBar()
    {
        InitializeComponent();
    }

    public event Action<string>? SearchTextChanged;

    public event Action<bool>? StepRequested;

    public string Text
    {
        get => SearchBox.Text;
        set => SearchBox.Text = value;
    }

    private DispatcherQueueTimer SearchTimer => field ??= CreateSearchTimer();

    private string PublishedSearch { get; set; } = string.Empty;

    public void FocusSearch() => SearchBox.Focus(FocusState.Programmatic);

    public void Clear()
    {
        SearchTimer.Stop();

        PublishedSearch = string.Empty;

        SearchBox.Text = string.Empty;
    }

    private string GetMatchCountText(int? count) => count switch
    {
        null => string.Empty,
        0 => "No Results",
        1 => "1 Result",
        _ => $"{count:N0} Results"
    };

    private Visibility GetMatchCountVisibility(int? count) => count is null ? Visibility.Collapsed : Visibility.Visible;

    private Visibility GetCommandsVisibility(object? commands) => commands is null ? Visibility.Collapsed : Visibility.Visible;

    private DispatcherQueueTimer CreateSearchTimer()
    {
        var timer = DispatcherQueue.CreateTimer();

        timer.Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds);
        timer.IsRepeating = false;

        timer.Tick += OnSearchTimerTick;

        return timer;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            SearchTimer.Start();

            return;
        }

        PublishSearch();
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!PublishSearch())
        {
            StepRequested?.Invoke(!KeyboardState.IsShiftDown());
        }
    }

    private void OnSearchTimerTick(DispatcherQueueTimer sender, object args) => PublishSearch();

    private bool PublishSearch()
    {
        SearchTimer.Stop();

        var text = SearchBox.Text ?? string.Empty;

        if (string.Equals(text, PublishedSearch, StringComparison.Ordinal))
        {
            return false;
        }

        PublishedSearch = text;

        SearchTextChanged?.Invoke(text);

        return true;
    }
}
