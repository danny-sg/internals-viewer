using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using SkiaSharp;

namespace InternalsViewer.UI.App.Services.Query.Trace;

public static class TraceTickTracks
{
    public static readonly SKColor LightBackground = new(0xFF, 0xFF, 0xFF, 0x40);

    private const string GetRowsMenuText = "GetRow/GetBatch";

    private const string BatchReturnsTitle = "GetBatch";

    private const float MarkTickWidth = 2f;

    private static readonly SKColor PassedUpColour = SKColor.Parse(SymbolCategory.QueryOperator.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor BatchColour = SKColor.Parse(SymbolCategory.ColumnStore.GetCategoryMetadata()!.ForegroundColor);

    public static SKColor DefaultColour => PassedUpColour;

    public static TraceTickTrack Create(TraceResult trace,
                                        IReadOnlyList<TraceTickDefinition> rules,
                                        TraceTickTrackSeries series,
                                        int ordinal,
                                        Func<TraceTickDefinition, bool> isAdded)
    {
        var colours = rules.Select(r => ParseColour(r.Colour)).ToArray();

        var categoryColours = new Dictionary<CallTreeNode, SKColor>(ReferenceEqualityComparer.Instance);

        var ruleByValue = GetRuleByValue(series.Values);

        var first = rules[0];

        var track = new TraceTickTrack(first.Name, TraceTickLocation.Bottom, ordinal, series.Series)
        {
            Values = [.. rules.Select((r, index) => new TraceTickValue(series.Values[index], r.GetLabel()))],
            GetColour = t => ruleByValue[t.Value] is >= 0 and var rule ? GetColour(rules[rule], colours[rule], t, categoryColours) : null,
            GetRule = t => ruleByValue[t.Value] is >= 0 and var rule ? rules[rule] : null,
            Colour = colours[0],
            Background = SKColor.TryParse(first.Background, out var background) ? background : isAdded(first) ? LightBackground : null,
            HeightFactor = (float)first.HeightFactor,
            WidthFactor = (float)first.WidthFactor,
            GetDetails = t => ruleByValue[t.Value] is >= 0 and var rule ? GetDetails(trace, rules[rule], t) : [],
            Rules = rules,
            IsCustom = rules.Any(isAdded)
        };

        return first.Source switch
        {
            TraceTickSourceType.RowReturn => WithRowReturns(track, trace.RowReturnMarks, rules, colours, ruleByValue),
            TraceTickSourceType.Seek => track with { TickWidth = MarkTickWidth },
            _ => track
        };
    }

    public static string GetSourceText(TraceTickSourceType source) => source switch
    {
        TraceTickSourceType.RowReturn => "GetRow Returns",
        TraceTickSourceType.RowAccess => "Row Access",
        TraceTickSourceType.Seek => "Seek",
        _ => string.Empty
    };

    public static SKColor ParseColour(string colour) => SKColor.TryParse(colour, out var parsed) ? parsed : DefaultColour;

    public static SKColor GetCategoryColour(CallTreeNode node) => node.CategoryColour is { } hex ? ParseColour(hex) : DefaultColour;

    private static SKColor GetColour(TraceTickDefinition rule,
                                     SKColor colour,
                                     TraceTick tick,
                                     Dictionary<CallTreeNode, SKColor> categoryColours)
    {
        if (rule.Colour != TraceTickDefinition.CategoryColour)
        {
            return colour;
        }

        if (!categoryColours.TryGetValue(tick.Node, out var category))
        {
            category = GetCategoryColour(tick.Node);

            categoryColours[tick.Node] = category;
        }

        return category;
    }

    private static int[] GetRuleByValue(byte[] values)
    {
        var rules = new int[byte.MaxValue + 1];

        Array.Fill(rules, -1);

        for (var index = values.Length - 1; index >= 0; index--)
        {
            rules[values[index]] = index;
        }

        return rules;
    }

    private static TraceTickTrack WithRowReturns(TraceTickTrack track,
                                                 TraceRowReturnMarkMap marks,
                                                 IReadOnlyList<TraceTickDefinition> rules,
                                                 SKColor[] colours,
                                                 int[] ruleByValue)
    {
        var returned = rules.ToList().FindIndex(r => r is { Source: TraceTickSourceType.RowReturn, Value: (byte)RowReturnType.Returned });

        var passedUp = returned >= 0 ? colours[returned] : PassedUpColour;

        return track with
        {
            Location = TraceTickLocation.Top,
            MenuText = GetRowsMenuText,
            GetColour = t => ruleByValue[t.Value] is >= 0 and var rule
                                 ? rule == returned && marks.IsBatch(t.Operator) ? BatchColour : colours[rule]
                                 : null,
            GetTrackColour = o => marks.IsBatch(o) ? BatchColour : passedUp,
            HasCallReturnLine = true,
            IsStatementTrack = true,
            TickWidth = MarkTickWidth,
            GetTitle = o => marks.IsBatch(o) ? BatchReturnsTitle : track.Name
        };
    }

    private static IEnumerable<string> GetDetails(TraceResult trace, TraceTickDefinition rule, TraceTick tick) => rule.Source switch
    {
        TraceTickSourceType.RowAccess => GetRowAccessDetails(tick, trace.PageAccesses, trace.BufferPool),
        TraceTickSourceType.Seek => GetSeekDetails(tick, trace.PageAccesses),
        TraceTickSourceType.Call => GetReadDetails(tick, trace.CallStore),
        _ => []
    };

    private static IEnumerable<string> GetSeekDetails(TraceTick tick, TracePageAccessList accesses)
    {
        if (accesses.GetFetch(tick.Span.From.Position) is { } fetch)
        {
            yield return $"Page {fetch.Page}";
        }
    }

    private static IEnumerable<string> GetRowAccessDetails(TraceTick tick, TracePageAccessList accesses, TraceBufferPool pool)
    {
        if (accesses.GetRowAccess(tick.Span.From.Position) is not { Offset: { } offset } access)
        {
            yield break;
        }

        yield return $"Page {access.Page}";

        yield return pool.GetLayout(access.Page, access.DatabaseId)?.GetSlot(offset) is { } slot ? $"Slot {slot}" : $"Offset 0x{offset:X}";
    }

    private static IEnumerable<string> GetReadDetails(TraceTick tick, TraceCallStore calls)
    {
        if (tick.Node.Frame is not { } frame
            || calls.GetCallList(tick.FunctionAddress, tick.InstanceAddress) is not { } list
            || tick.CallIndex >= list.Count
            || TraceReadResolver.GetRead(frame, list[tick.CallIndex]) is not { } read)
        {
            yield break;
        }

        yield return read.IsReadAhead
            ? $"{read.PageCount:N0} {(read.PageCount == 1 ? "Page" : "Pages")} From {read.Page}"
            : $"Page {read.Page}";
    }
}
