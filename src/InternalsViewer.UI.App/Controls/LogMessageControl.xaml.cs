using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Services.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;

namespace InternalsViewer.UI.App.Controls.Logging;

public sealed partial class LogMessageControl : UserControl
{
    public static readonly DependencyProperty EntryProperty =
        DependencyProperty.Register(nameof(Entry), typeof(LogEntry), typeof(LogMessageControl),
            new PropertyMetadata(null, OnEntryChanged));

    public LogEntry? Entry
    {
        get => (LogEntry?)GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    private TextBlock MessageBlock { get; } = new() { FontFamily = new("Consolas"), FontSize = 11, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

    public LogMessageControl()
    {
        Content = MessageBlock;
    }

    private static void OnEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LogMessageControl)d).Rebuild();

    private void Rebuild()
    {
        MessageBlock.Inlines.Clear();

        if (Entry is null)
        {
            return;
        }

        var parameters = Entry.Parameters;

        if (parameters is null)
        {
            MessageBlock.Inlines.Add(new Run { Text = Entry.Message });
            return;
        }

        // Build a lookup of name -> value from structured parameters (skip {OriginalFormat})
        var lookup = new Dictionary<string, object?>();

        foreach (var (key, value) in parameters)
        {
            if (key != "{OriginalFormat}")
            {
                lookup[key] = value;
            }
        }

        var template = Entry.Message;
        var lastIndex = 0;

        foreach (var (name, value) in lookup)
        {
            if (value is not PageAddress pageAddress)
            {
                continue;
            }

            var token = pageAddress.ToString();
            var index = template.IndexOf(token, lastIndex, StringComparison.Ordinal);

            if (index < 0)
            {
                continue;
            }

            if (index > lastIndex)
            {
                MessageBlock.Inlines.Add(new Run { Text = template[lastIndex..index] });
            }

            var link = new Hyperlink();
            link.Inlines.Add(new Run { Text = token });
          //  link.Click += (_, _) => WeakReferenceMessenger.Default.Send(new OpenPageMessage(new() {P}pageAddress));
            MessageBlock.Inlines.Add(link);

            lastIndex = index + token.Length;
        }

        if (lastIndex < template.Length)
        {
            MessageBlock.Inlines.Add(new Run { Text = template[lastIndex..] });
        }
    }
}