using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Helpers;
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

public static partial class TraceTickTracks
{
    public static readonly SKColor LightBackground = new(0xFF, 0xFF, 0xFF, 0x40);

    private const string GetRowsMenuText = "GetRow/GetBatch";

    private const string BatchReturnsTitle = "GetBatch";

    private const float MarkTickWidth = 2f;

    private const string BoldMarker = "**";

    private const string CodeMarker = "`";

    private const string UnknownValue = "?";

    private const string PageLinkScheme = "page";

    private static readonly SKColor PassedUpColour = SKColor.Parse(SymbolCategory.QueryOperator.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor BatchColour = SKColor.Parse(SymbolCategory.ColumnStore.GetCategoryMetadata()!.ForegroundColor);

    public static SKColor DefaultColour => PassedUpColour;

    public static TraceTickTrack Create(TraceResult trace,
                                        IReadOnlyList<TraceTickDefinition> rules,
                                        TraceTickTrackSeries series,
                                        int ordinal,
                                        Func<TraceTickDefinition, bool> isAdded,
                                        Func<TraceTick, string> getParentName)
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
            GetPlaceholder = (t, name) => GetPlaceholder(trace, t, name, getParentName),
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
        TraceTickSourceType.PageFetch => "Page Fetch",
        TraceTickSourceType.Open => "Operator Open",
        _ => string.Empty
    };

    public static string? GetText(TraceTickTrack track, TraceTick tick, string? text)
        => string.IsNullOrEmpty(text) ? null : TraceTickDefinition.Format(text, name => track.GetPlaceholder(tick, name));

    public static List<TraceTrackStep> GetSteps(IEnumerable<TraceTickTrack> tracks, IReadOnlySet<string>? hidden)
    {
        var steps = new List<TraceTrackStep>();

        foreach (var track in tracks)
        {
            if (hidden?.Contains(track.Name) == true)
            {
                continue;
            }

            foreach (var tick in track.Series.GetTicks(static _ => true))
            {
                if (track.GetRule(tick) is not { } rule)
                {
                    continue;
                }

                steps.Add(new TraceTrackStep(track, tick, rule, IsReturn: false));

                if (!string.IsNullOrEmpty(rule.ReturnDescription))
                {
                    steps.Add(new TraceTrackStep(track, tick, rule, IsReturn: true));
                }
            }
        }

        steps.Sort(static (left, right) => left.Point.Position.CompareTo(right.Point.Position));

        return steps;
    }

    public static int GetStepIndex(List<TraceTrackStep> steps, TracePosition? current, bool forward)
    {
        if (current is not { } position)
        {
            return forward && steps.Count > 0 ? 0 : -1;
        }

        ReadOnlySpan<TraceTrackStep> span = CollectionsMarshal.AsSpan(steps);

        if (!forward)
        {
            return FirstStepAtOrAfter(span, position) - 1;
        }

        var next = SortedSearch.FirstAfter<TraceTrackStep, TracePosition>(span, position, GetStepPosition);

        return next < span.Length ? next : -1;
    }

    public static int GetLatestStepIndex(List<TraceTrackStep> steps, TracePosition position)
        => SortedSearch.FirstAfter<TraceTrackStep, TracePosition>(CollectionsMarshal.AsSpan(steps), position, GetStepPosition) - 1;

    public static int GetPhaseStepIndex(List<TraceTrackStep> steps, int current, bool forward)
    {
        ReadOnlySpan<TraceTrackStep> span = CollectionsMarshal.AsSpan(steps);

        var target = -1;

        for (var index = 0; index < span.Length; index++)
        {
            if (!IsPhaseStart(span[index]))
            {
                continue;
            }

            target = GetNearerBoundary(target, index, current, forward);

            var end = FirstStepAtOrAfter(span, span[index].Tick.Span.To.Position);

            if (end < span.Length)
            {
                target = GetNearerBoundary(target, end, current, forward);
            }
        }

        return target;
    }

    public static string GetStepName(TraceTrackStep step) => GetText(step.Track, step.Tick, step.Rule.GetTitle()) ?? string.Empty;

    public static string GetStepDescription(TraceTrackStep step)
        => GetText(step.Track, step.Tick, step.IsReturn ? step.Rule.ReturnDescription : step.Rule.Description) ?? string.Empty;

    public static string ToPlainText(string markdown)
        => MarkdownLinkRegex().Replace(markdown, "$1").Replace(BoldMarker, string.Empty).Replace(CodeMarker, string.Empty);

    public static string GetPageLink(PageAddress page, int? slot)
    {
        var target = slot is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"{page.FileId}:{page.PageId}:{value}")
            : string.Create(CultureInfo.InvariantCulture, $"{page.FileId}:{page.PageId}");

        return $"[{page}]({PageLinkScheme}:{target})";
    }

    public static bool TryGetPageLink(Uri? uri, out PageAddress page, out ushort? slot)
    {
        page = PageAddress.Empty;

        slot = null;

        if (uri is null || !uri.Scheme.Equals(PageLinkScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.OriginalString[(PageLinkScheme.Length + 1)..].Split(':');

        if (parts.Length < 2
            || !short.TryParse(parts[0], CultureInfo.InvariantCulture, out var fileId)
            || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var pageId))
        {
            return false;
        }

        page = new PageAddress(fileId, pageId);

        if (parts.Length > 2 && ushort.TryParse(parts[2], CultureInfo.InvariantCulture, out var slotId))
        {
            slot = slotId;
        }

        return true;
    }

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

    private static string? GetPlaceholder(TraceResult trace, TraceTick tick, string name, Func<TraceTick, string> getParentName)
        => name switch
        {
            _ when IsPlaceholder(name, TraceTickDefinition.OperatorNamePlaceholder) => tick.Operator.OperatorDescription,
            _ when IsPlaceholder(name, TraceTickDefinition.ParentOperatorNamePlaceholder) => getParentName(tick),
            _ when IsPlaceholder(name, TraceTickDefinition.FunctionPlaceholder) => tick.Node.Symbol,
            _ when IsPlaceholder(name, TraceTickDefinition.PagePlaceholder)
                => GetPage(trace, tick) is { } page ? GetPageLink(page, GetSlot(trace, tick)) : UnknownValue,
            _ when IsPlaceholder(name, TraceTickDefinition.SlotPlaceholder)
                => GetSlot(trace, tick)?.ToString(CultureInfo.CurrentCulture) ?? UnknownValue,
            _ => null
        };

    private static bool IsPlaceholder(string name, string placeholder) => name.Equals(placeholder, StringComparison.OrdinalIgnoreCase);

    private static PageAddress? GetPage(TraceResult trace, TraceTick tick)
        => trace.PageAccesses.GetRowAccess(tick.Span.From.Position)?.Page
           ?? trace.PageAccesses.GetFetch(tick.Span.From.Position)?.Page
           ?? GetRead(tick, trace.CallStore)?.Page;

    private static int? GetSlot(TraceResult trace, TraceTick tick)
        => trace.PageAccesses.GetRowAccess(tick.Span.From.Position) is { Offset: { } offset } access
            ? trace.BufferPool.GetLayout(access.Page, access.DatabaseId)?.GetSlot(offset)
            : null;

    private static (PageAddress Page, int PageCount, bool IsReadAhead)? GetRead(TraceTick tick, TraceCallStore calls)
        => tick.Node.Frame is { } frame
           && calls.GetCallList(tick.FunctionAddress, tick.InstanceAddress) is { } list
           && tick.CallIndex < list.Count
            ? TraceReadResolver.GetRead(frame, list[tick.CallIndex])
            : null;

    private static TracePosition GetStepPosition(TraceTrackStep step) => step.Point.Position;

    private static bool IsPhaseStart(TraceTrackStep step) => step.Rule.IsPhase && !step.IsReturn;

    private static int FirstStepAtOrAfter(ReadOnlySpan<TraceTrackStep> steps, TracePosition position)
        => SortedSearch.FirstAtOrAfter<TraceTrackStep, TracePosition>(steps, position, GetStepPosition);

    private static int GetNearerBoundary(int target, int boundary, int current, bool forward)
    {
        if (forward ? boundary <= current : boundary >= current)
        {
            return target;
        }

        return target < 0 || (forward ? boundary < target : boundary > target) ? boundary : target;
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
        TraceTickSourceType.Seek or TraceTickSourceType.PageFetch => GetSeekDetails(tick, trace.PageAccesses),
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
        if (GetRead(tick, calls) is not { } read)
        {
            yield break;
        }

        yield return read.IsReadAhead
            ? $"{read.PageCount:N0} {(read.PageCount == 1 ? "Page" : "Pages")} From {read.Page}"
            : $"Page {read.Page}";
    }

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownLinkRegex();
}
