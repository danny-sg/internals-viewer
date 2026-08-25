using System.Collections.Generic;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// Something that a derivation drawn beneath it can hand a click back to
/// </summary>
/// <remarks>
/// The text is drawn into a plain text block rather than a control of its own, so there is nothing in the cell to
/// raise an event. The click walks up to whatever is hosting the grid instead.
/// </remarks>
public interface IDerivationNavigator
{
    void OnStepInvoked(DerivationStep step);

    void OnResultInvoked(ValueDerivation derivation);
}

public static class DerivationText
{
    private const int BadgeFontSize = 11;

    private static int _built;

    private static ILogger? _logger;

    private static Palette? _palette;

    private static ElementTheme _paletteTheme = ElementTheme.Default;

    public static readonly DependencyProperty DerivationProperty
        = DependencyProperty.RegisterAttached("Derivation",
                                              typeof(ValueDerivation),
                                              typeof(DerivationText),
                                              new PropertyMetadata(null, OnChanged));

    public static ValueDerivation? GetDerivation(DependencyObject element)
        => (ValueDerivation?)element.GetValue(DerivationProperty);

    public static void SetDerivation(DependencyObject element, ValueDerivation? value)
        => element.SetValue(DerivationProperty, value);

    public static readonly DependencyProperty ShowStepsProperty
        = DependencyProperty.RegisterAttached("ShowSteps",
                                              typeof(bool),
                                              typeof(DerivationText),
                                              new PropertyMetadata(true, OnChanged));

    public static bool GetShowSteps(DependencyObject element) => (bool)element.GetValue(ShowStepsProperty);

    public static void SetShowSteps(DependencyObject element, bool value) => element.SetValue(ShowStepsProperty, value);

    private static ILogger Logger => _logger ??= App.GetService<ILoggerFactory>().CreateLogger(nameof(DerivationText));

    private static void Build(TextBlock text)
    {
        text.Inlines.Clear();

        text.TextHighlighters.Clear();

        if (GetDerivation(text) is not { } derivation)
        {
            text.Visibility = Visibility.Collapsed;

            return;
        }

        text.Visibility = Visibility.Visible;

        using var timing = Logger.Time("Build derivation text", $"#{++_built}");

        var palette = GetPalette(text);

        var position = 0;

        var names = new List<TextRange>();

        var values = new List<TextRange>();

        if (GetShowSteps(text))
        {
            foreach (var step in derivation.Steps)
            {
                Append(text, step, palette, ref position, names, values);
            }

            if (derivation.Steps.Count > 0)
            {
                Add(text, new Run { Text = " = ", Foreground = palette.EqualsBrush }, ref position);
            }
        }

        AddResult(text, derivation, ref position);
    }

    /// <summary>
    /// One operand, being the operator that brought it in and the split badge naming it and what it held
    /// </summary>
    private static void Append(TextBlock text,
                               DerivationStep step,
                               Palette palette,
                               ref int position,
                               List<TextRange> names,
                               List<TextRange> values)
    {
        if (step.Operator.Length > 0)
        {
            Add(text,
                new Run { Text = $" {step.Operator} ", Foreground = palette.OperatorBrush },
                ref position);
        }

        var name = Badge($" {step.Name} ");

        var value = Badge($" {step.Value} ");

        names.Add(Range(position, name.Text.Length));

        values.Add(Range(position + name.Text.Length, value.Text.Length));

        if (!step.IsNavigable)
        {
            Add(text, name, ref position);

            Add(text, value, ref position);

            return;
        }

        var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };

        link.Inlines.Add(name);
        link.Inlines.Add(value);

        link.Click += (sender, _) => Navigator(sender)?.OnStepInvoked(step);

        text.Inlines.Add(link);

        position += name.Text.Length + value.Text.Length;
    }

    private static void AddResult(TextBlock text, ValueDerivation derivation, ref int position)
    {
        if (!derivation.IsNavigable)
        {
            Add(text, new Run { Text = derivation.Result }, ref position);

            return;
        }

        var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };

        link.Inlines.Add(new Run { Text = derivation.Result });

        link.Click += (sender, _) => Navigator(sender)?.OnResultInvoked(derivation);

        text.Inlines.Add(link);

        position += derivation.Result.Length;
    }

    /// <summary>
    /// Whatever is hosting the cell the link was drawn in, found by walking up from the link itself
    /// </summary>
    private static IDerivationNavigator? Navigator(Hyperlink link)
    {
        DependencyObject? current = link.ContentStart.VisualParent;

        while (current is not null)
        {
            if (current is IDerivationNavigator navigator)
            {
                return navigator;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static Run Badge(string text) => new() { Text = text, FontSize = BadgeFontSize };

    private static void Add(TextBlock text, Run run, ref int position)
    {
        text.Inlines.Add(run);

        position += run.Text.Length;
    }

    /// <summary>
    /// Paints the badges, a highlighter carrying the colours a border and its own text block used to
    /// </summary>
    private static void Highlight(TextBlock text, List<TextRange> ranges, Brush? background, Brush? foreground)
    {
        if (ranges.Count == 0)
        {
            return;
        }

        var highlighter = new TextHighlighter
        {
            Background = background,
            Foreground = foreground
        };

        foreach (var range in ranges)
        {
            highlighter.Ranges.Add(range);
        }

        text.TextHighlighters.Add(highlighter);
    }

    private static TextRange Range(int start, int length) => new() { StartIndex = start, Length = length };

    private static Palette GetPalette(TextBlock text)
    {
        if (_palette is { } palette && _paletteTheme == text.ActualTheme)
        {
            return palette;
        }

        _paletteTheme = text.ActualTheme;

        return _palette = Palette.Read();
    }

    private sealed class Palette
    {
        public required Brush? OperatorBrush { get; init; }

        public required Brush? EqualsBrush { get; init; }

        public required Brush? BadgeTextBrush { get; init; }

        public required Brush? NameBadgeBrush { get; init; }

        public required Brush? ValueBadgeBrush { get; init; }

        public static Palette Read() => new()
        {
            OperatorBrush = GetBrush("TextFillColorSecondaryBrush"),
            EqualsBrush = GetBrush("TextFillColorTertiaryBrush"),
            BadgeTextBrush = GetBrush("TextOnAccentFillColorPrimaryBrush"),
            NameBadgeBrush = GetBrush("ControlStrongFillColorDefaultBrush"),
            ValueBadgeBrush = GetBrush("AccentFillColorDefaultBrush")
        };

        private static Brush? GetBrush(string key)
            => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as Brush : null;
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock text)
        {
            Build(text);
        }
    }
}
