using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using Microsoft.UI.Xaml;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const byte SearchDimAlpha = 110;

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText),
                                    typeof(string),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnSearchTextChanged));

    public string? SearchText
    {
        get => (string?)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly DependencyProperty HasSearchMatchesProperty =
        DependencyProperty.Register(nameof(HasSearchMatches), typeof(bool), typeof(TraceTimelineControl), new PropertyMetadata(false));

    public bool HasSearchMatches
    {
        get => (bool)GetValue(HasSearchMatchesProperty);
        private set => SetValue(HasSearchMatchesProperty, value);
    }

    private bool[]? _searchMatches;

    private bool[]? _nodeMatches;

    public void StepSearch(bool forward)
    {
        if (_searchMatches is { } matches)
        {
            StepTo(forward, matches, allRows: true);
        }
    }

    private void FindSearchMatches()
    {
        _colours.Clear();

        _searchMatches = null;

        HasSearchMatches = false;

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

        HasSearchMatches = Array.IndexOf(matches, true) >= 0;
    }

    private void FindNodeMatches(CallStackNode? selected)
    {
        _colours.Clear();

        _nodeMatches = null;

        if (selected is null || _timeline is not { } timeline)
        {
            return;
        }

        HashSet<CallStackNode> members = [.. selected.MergedFrom.Count > 0 ? selected.MergedFrom : [selected]];

        _nodeMatches = CallsUnder(timeline, members.Contains)
                       ?? (selected.Frame is { } frame ? CallsUnder(timeline, n => ReferenceEquals(n.Frame, frame)) : null);
    }

    private static bool[]? CallsUnder(TraceTimeline timeline, Func<CallStackNode, bool> isSelected)
    {
        var under = new bool[timeline.NodeCount];

        var any = false;

        for (var node = 0; node < under.Length; node++)
        {
            var parent = timeline.ParentOf(node);

            under[node] = (parent >= 0 && under[parent]) || (timeline.NodeOf(node) is { } callNode && isSelected(callNode));

            any |= under[node];
        }

        return any ? under : null;
    }

    private bool IsDimmed(int node) => Excludes(_searchMatches, node);

    private static bool Excludes(bool[]? matches, int node) => matches is not null && node >= 0 && node < matches.Length && !matches[node];

    private static SKColor Dimmed(SKColor colour)
    {
        var grey = (byte)(colour.Red * 0.299 + colour.Green * 0.587 + colour.Blue * 0.114);

        return new SKColor(grey, grey, grey, SearchDimAlpha);
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control.FindSearchMatches();

        control.Redraw();
    }
}
