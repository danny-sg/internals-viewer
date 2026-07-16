using System;
using System.Collections.Generic;
using System.Xml.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Services.Logging;
using InternalsViewer.Query.Events;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

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

    private TextBlock MessageBlock { get; } = new()
    {
        FontFamily = new("Consolas"),
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true
    };

    private Button ExpandCollapseButton { get; } = new()
    {
        Content = "Expand",
        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
        Visibility = Microsoft.UI.Xaml.Visibility.Collapsed
    };

    private bool IsExpanded { get; set; }

    public LogMessageControl()
    {
        ExpandCollapseButton.Click += OnExpandCollapseClick;

        var toolbar = new StackPanel
        {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                ExpandCollapseButton
            }
        };

        Content = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                toolbar,
                MessageBlock
            }
        };
    }

    private static void OnEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LogMessageControl)d).Rebuild();

    private void Rebuild()
    {
        IsExpanded = false;
        RenderEntry();
    }

    private void RenderEntry()
    {
        MessageBlock.Inlines.Clear();

        if (Entry is null)
        {
            ExpandCollapseButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            return;
        }

        if (TryGetXEventPayload(Entry, out var payload))
        {
            ExpandCollapseButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            ExpandCollapseButton.Content = IsExpanded ? "Collapse" : "Expand";

            MessageBlock.TextWrapping = IsExpanded ? TextWrapping.Wrap : TextWrapping.NoWrap;
            MessageBlock.TextTrimming = IsExpanded ? TextTrimming.None : TextTrimming.CharacterEllipsis;
            MessageBlock.MaxLines = IsExpanded ? 0 : 1;

            var text = BuildPayloadText(payload, IsExpanded);
            MessageBlock.Inlines.Add(new Run { Text = text });
            return;
        }

        ExpandCollapseButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        MessageBlock.TextWrapping = TextWrapping.Wrap;
        MessageBlock.TextTrimming = TextTrimming.None;
        MessageBlock.MaxLines = 0;

        var parameters = Entry.Parameters;

        if (parameters is null)
        {
            MessageBlock.Inlines.Add(new Run { Text = Entry.Message });
            return;
        }

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

        foreach (var (_, value) in lookup)
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
            MessageBlock.Inlines.Add(link);

            lastIndex = index + token.Length;
        }

        if (lastIndex < template.Length)
        {
            MessageBlock.Inlines.Add(new Run { Text = template[lastIndex..] });
        }
    }

    private void OnExpandCollapseClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (Entry is null || !TryGetXEventPayload(Entry, out _))
        {
            return;
        }

        IsExpanded = !IsExpanded;
        RenderEntry();
    }

    private static string BuildPayloadText(XEventPayload payload, bool expanded)
    {
        var xml = expanded ? FormatXml(payload.Value) : CollapseXml(payload.Value);

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return xml;
        }

        return expanded
            ? payload.Name + Environment.NewLine + xml
            : payload.Name + " " + xml;
    }

    private static bool TryGetXEventPayload(LogEntry entry, out XEventPayload payload)
    {
        if (entry.Parameters is not { } parameters)
        {
            payload = default;
            return false;
        }

        foreach (var (_, value) in parameters)
        {
            if (value is XEventPayload xeventPayload)
            {
                payload = xeventPayload;
                return true;
            }
        }

        payload = default;
        return false;
    }

    private static string FormatXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);
            return document.ToString(SaveOptions.None);
        }
        catch
        {
            return xml;
        }
    }

    private static string CollapseXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);
            return document.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            return xml.Replace("\r", string.Empty).Replace("\n", " ").Trim();
        }
    }
}
