using System;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.CallStack;

/// <summary>
/// Highlights every occurrence of a search text in a text block
/// </summary>
/// <remarks>
/// Works on text blocks drawn from runs too, such as a signature, since a highlighter addresses the block's whole
/// text by index. The highlight source is the signature where <see cref="SignatureText"/> set one, otherwise the
/// block's own text. A range the block cannot take, which can happen while a recycled row is between one text and
/// the next, is dropped rather than left to fault the layout: a highlight is decoration, never worth a crash.
/// </remarks>
public static class SearchHighlight
{
    public static readonly DependencyProperty SearchProperty
        = DependencyProperty.RegisterAttached("Search", typeof(string), typeof(SearchHighlight), new PropertyMetadata(null, OnChanged));

    public static string? GetSearch(DependencyObject element) => (string?)element.GetValue(SearchProperty);

    public static void SetSearch(DependencyObject element, string? value) => element.SetValue(SearchProperty, value);

    public static void Apply(TextBlock text)
    {
        text.TextHighlighters.Clear();

        var source = SignatureText.GetSignature(text) ?? text.Text;

        var ranges = SearchHighlightRanges.Find(source, GetSearch(text));

        if (ranges.Count == 0)
        {
            return;
        }

        var highlighter = new TextHighlighter
        {
            Background = Brush("SearchHighlightBrush"),
            Foreground = Brush("SearchHighlightForegroundBrush")
        };

        try
        {
            foreach (var (start, length) in ranges)
            {
                highlighter.Ranges.Add(new TextRange { StartIndex = start, Length = length });
            }

            text.TextHighlighters.Add(highlighter);
        }
        catch (ArgumentException)
        {
            text.TextHighlighters.Clear();
        }
    }

    private static Brush? Brush(string key)
        => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as Brush : null;

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text)
        {
            return;
        }

        if (e.OldValue is null)
        {
            text.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) => Apply(text));
        }

        Apply(text);
    }
}
