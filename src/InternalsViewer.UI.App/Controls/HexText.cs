using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls;

public static partial class HexText
{
    public static readonly DependencyProperty TextProperty
        = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(HexText), new PropertyMetadata(null, OnTextChanged));

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static void Apply(TextBlock textBlock, string? text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("0x"))
        {
            textBlock.Text = text ?? string.Empty;

            return;
        }

        textBlock.Inlines.Clear();

        AddRuns(textBlock.Inlines, text);
    }

    public static void AddRuns(InlineCollection inlines, string text)
    {
        var start = 0;

        foreach (Match match in HexValue().Matches(text))
        {
            if (match.Index > start)
            {
                inlines.Add(new Run { Text = text[start..match.Index] });
            }

            inlines.Add(new Run { Text = match.Value, FontFamily = Monospace });

            start = match.Index + match.Length;
        }

        if (start < text.Length)
        {
            inlines.Add(new Run { Text = text[start..] });
        }
    }

    private static FontFamily? Monospace
        => Application.Current.Resources.TryGetValue("MonospaceFontFamily", out var resource) ? resource as FontFamily : null;

    [GeneratedRegex(@"\b0x[0-9A-Fa-f]+\b")]
    private static partial Regex HexValue();

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
        {
            Apply(textBlock, e.NewValue as string);
        }
    }
}
