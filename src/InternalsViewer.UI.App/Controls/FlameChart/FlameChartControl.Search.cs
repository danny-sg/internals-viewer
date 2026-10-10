using System;
using System.Linq;
using Microsoft.UI.Xaml;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const byte SearchDimAlpha = 110;

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText),
                                    typeof(string),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnSearchTextChanged));

    public string? SearchText
    {
        get => (string?)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    private bool[]? _searchMatches;

    private void FindSearchMatches()
    {
        _colours.Clear();

        _searchMatches = null;

        var terms = SearchText?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (terms.Length == 0 || _timeline is not { } timeline)
        {
            return;
        }

        var matches = new bool[timeline.NodeCount];

        for (var node = 0; node < matches.Length; node++)
        {
            var label = LabelOf(node);

            matches[node] = terms.All(t => label.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        _searchMatches = matches;
    }

    private bool IsDimmed(int node) => _searchMatches is { } matches && node >= 0 && node < matches.Length && !matches[node];

    private static SKColor Dimmed(SKColor colour)
    {
        var grey = (byte)(colour.Red * 0.299 + colour.Green * 0.587 + colour.Blue * 0.114);

        return new SKColor(grey, grey, grey, SearchDimAlpha);
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        control.FindSearchMatches();

        control.Redraw();
    }
}
