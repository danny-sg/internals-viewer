using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using InternalsViewer.UI.App.Models.Query;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Query.SqlEditor;

public sealed class QueryMessagesControl : UserControl
{
    private const double ProgressWidth = 160;

    private const double ProgressHeight = 12;

    public static readonly DependencyProperty MessagesProperty =
        DependencyProperty.Register(nameof(Messages),
                                    typeof(ObservableCollection<QueryMessage>),
                                    typeof(QueryMessagesControl),
                                    new PropertyMetadata(null, OnMessagesChanged));

    public ObservableCollection<QueryMessage>? Messages
    {
        get => (ObservableCollection<QueryMessage>?)GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    public QueryMessagesControl()
    {
        Scroller.Content = Text;

        Content = Scroller;

        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => Text.Foreground = Foreground);
    }

    private RichTextBlock Text { get; } = new()
    {
        FontFamily = new FontFamily("Courier New"),
        IsTextSelectionEnabled = true,
        TextWrapping = TextWrapping.Wrap,
        Padding = new Thickness(8)
    };

    private ScrollViewer Scroller { get; } = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private Dictionary<QueryMessage, ProgressLine> ProgressLines { get; } = [];

    private bool IsScrollQueued { get; set; }

    private bool IsAtEnd => Scroller.VerticalOffset >= Scroller.ScrollableHeight - 1;

    private static string PercentageText(QueryMessage message) => message.Percentage is { } percentage ? $"{percentage:N0}%" : string.Empty;

    private static string FinishedText(QueryMessage message) => $"{message.Text} - {message.Outcome}";

    private void Attach(ObservableCollection<QueryMessage>? previous, ObservableCollection<QueryMessage>? messages)
    {
        if (previous is not null)
        {
            previous.CollectionChanged -= OnCollectionChanged;
        }

        if (messages is not null)
        {
            messages.CollectionChanged += OnCollectionChanged;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        Clear();

        if (Messages is { } messages)
        {
            foreach (var message in messages)
            {
                Text.Blocks.Add(Render(message));
            }
        }

        QueueScrollToEnd();
    }

    private void Clear()
    {
        foreach (var message in ProgressLines.Keys)
        {
            message.PropertyChanged -= OnMessagePropertyChanged;
        }

        ProgressLines.Clear();

        Text.Blocks.Clear();
    }

    private Paragraph Render(QueryMessage message)
    {
        var paragraph = new Paragraph();

        if (message.IsFinished)
        {
            paragraph.Inlines.Add(new Run { Text = FinishedText(message) });

            return paragraph;
        }

        paragraph.Inlines.Add(new Run { Text = message.Text });

        if (!message.IsProgress)
        {
            return paragraph;
        }

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = message.Percentage ?? 0,
            IsIndeterminate = message.Percentage is null,
            VerticalAlignment = VerticalAlignment.Center
        };

        var percentage = new Run { Text = PercentageText(message) };

        paragraph.Inlines.Add(new InlineUIContainer
        {
            Child = new Grid
            {
                Width = ProgressWidth,
                Height = ProgressHeight,
                Margin = new Thickness(8, 0, 8, 0),
                Children = { bar }
            }
        });

        paragraph.Inlines.Add(percentage);

        ProgressLines[message] = new ProgressLine(paragraph, bar, percentage);

        message.PropertyChanged += OnMessagePropertyChanged;

        return paragraph;
    }

    private void QueueScrollToEnd()
    {
        if (IsScrollQueued)
        {
            return;
        }

        IsScrollQueued = true;

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            IsScrollQueued = false;

            Scroller.ChangeView(null, Scroller.ScrollableHeight, null, true);
        });
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e is not { Action: NotifyCollectionChangedAction.Add, NewItems: { } items } || e.NewStartingIndex != Text.Blocks.Count)
        {
            Rebuild();

            return;
        }

        var isAtEnd = IsAtEnd;

        foreach (QueryMessage message in items)
        {
            Text.Blocks.Add(Render(message));
        }

        if (isAtEnd)
        {
            QueueScrollToEnd();
        }
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not QueryMessage message || !ProgressLines.TryGetValue(message, out var line))
        {
            return;
        }

        if (message.IsFinished)
        {
            message.PropertyChanged -= OnMessagePropertyChanged;

            ProgressLines.Remove(message);

            line.Paragraph.Inlines.Clear();

            line.Paragraph.Inlines.Add(new Run { Text = FinishedText(message) });

            return;
        }

        line.Bar.IsIndeterminate = message.Percentage is null;

        line.Bar.Value = message.Percentage ?? 0;

        line.Percentage.Text = PercentageText(message);
    }

    private static void OnMessagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((QueryMessagesControl)d).Attach(e.OldValue as ObservableCollection<QueryMessage>,
                                            e.NewValue as ObservableCollection<QueryMessage>);

    private sealed record ProgressLine(Paragraph Paragraph, ProgressBar Bar, Run Percentage);
}
