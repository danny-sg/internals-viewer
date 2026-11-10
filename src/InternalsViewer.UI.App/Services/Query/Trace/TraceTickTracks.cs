using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using SkiaSharp;

namespace InternalsViewer.UI.App.Services.Query.Trace;

public static partial class TraceTickTracks
{
    public static readonly SKColor LightBackground = new(0xFF, 0xFF, 0xFF, 0x40);

    private const string GetRowsMenuText = "GetRow/GetBatch";

    private const string BatchReturnsTitle = "GetBatch";

    private const string RowReturnsTitle = "GetRow";

    private const string BatchUnit = "batch";

    private const string RowUnit = "row";

    private const float MarkTickWidth = 2f;

    private const string BoldMarker = "**";

    private const string CodeMarker = "`";

    private const string UnknownValue = "?";

    private const string PageLinkScheme = "page";

    private const string PredicateOpenTag = "<Predicate>";

    private const string PredicateCloseTag = "</Predicate>";

    private static readonly SKColor PassedUpColour = SKColor.Parse(SymbolCategory.QueryOperator.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor BatchColour = SKColor.Parse(SymbolCategory.ColumnStore.GetCategoryMetadata()!.ForegroundColor);

    public static SKColor DefaultColour => PassedUpColour;

    public static TraceTickTrack Create(TraceResult trace,
                                        IReadOnlyList<TraceTickDefinition> rules,
                                        TraceTickTrackSeries series,
                                        int ordinal,
                                        Func<TraceTickDefinition, bool> isAdded,
                                        Func<TraceTick, string> getParentName,
                                        Func<TraceTick, string, PredicateText?> getPredicate,
                                        Func<TraceTickDefinition, TraceWatch> getWatch)
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
            GetPlaceholder = (t, name) => GetPlaceholder(trace, t, name, getParentName, getPredicate),
            GetPredicate = getPredicate,
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
            TraceTickSourceType.Statement => track with { IsStatementTrack = true },
            TraceTickSourceType.Watch => WithWatches(track, rules, ruleByValue, getWatch),
            _ => track
        };
    }

    public static string GetSourceText(TraceTickSourceType source) => source switch
    {
        TraceTickSourceType.RowReturn => "GetRow Returns",
        TraceTickSourceType.RowAccess => "Row Access",
        TraceTickSourceType.PageHeaderAccess => "Page Header Access",
        TraceTickSourceType.Seek => "Seek",
        TraceTickSourceType.PageFetch => "Page Fetch",
        TraceTickSourceType.Open => "Operator Open",
        TraceTickSourceType.Watch => "Watch",
        TraceTickSourceType.Evaluate => "Compute Scalar Evaluate",
        TraceTickSourceType.Statement => "Statement Call",
        _ => string.Empty
    };

    public static string GetWatchTarget(TraceTickDefinition definition) => $"{definition.Function} + 0x{definition.Offset:X}";

    public static string FormatWatchValue(TraceTickDefinition definition, byte[] value)
        => MemoryWatchRowViewModel.GetValue(new HexWatch(0, value.Length, definition.DataType), value);

    public static string? GetText(TraceTickTrack track, TraceTick tick, string? text)
        => string.IsNullOrEmpty(text)
            ? null
            : TraceTickDefinition.Format(text, name => track.GetPlaceholder(tick, name))
                                 .Replace(PredicateOpenTag, string.Empty)
                                 .Replace(PredicateCloseTag, string.Empty)
                                 .TrimEnd();

    public static List<TraceTrackStep> GetSteps(IEnumerable<TraceTickTrack> tracks, IReadOnlySet<string>? hidden)
    {
        var steps = new List<TraceTrackStep>();

        foreach (var track in tracks)
        {
            if (hidden?.Contains(track.Name) == true || track.GetText is not null)
            {
                continue;
            }

            foreach (var tick in track.Series.GetTicks(static _ => true))
            {
                if (track.GetRule(tick) is not { } rule || IsForwardingCall(tick, rule))
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

    public static TraceStepNesting[] GetStepNesting(List<TraceTrackStep> steps)
    {
        var nesting = new TraceStepNesting[steps.Count];

        var open = new Dictionary<uint, List<int>>();

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];

            if (!open.TryGetValue(step.Tick.Thread, out var calls))
            {
                calls = [];

                open[step.Tick.Thread] = calls;
            }

            RemoveEndedSeeks(steps, calls, step);

            var isNesting = IsNestingStep(step);

            if (step.IsReturn && isNesting && calls.FindLastIndex(c => IsSameTick(steps[c], step)) is >= 0 and var position)
            {
                var call = calls[position];

                calls.RemoveRange(position, calls.Count - position);

                nesting[index] = new TraceStepNesting(call, index);

                nesting[call] = nesting[call] with { End = index };

                continue;
            }

            var parent = calls.Count > 0 ? calls[^1] : -1;

            nesting[index] = new TraceStepNesting(parent, index);

            if (parent >= 0)
            {
                nesting[parent] = nesting[parent] with { End = index };
            }

            if (!step.IsReturn && isNesting)
            {
                calls.Add(index);
            }
        }

        return nesting;
    }

    public static bool IsStickyStep(TraceTrackStep step, int index, TraceStepNesting[] nesting)
        => step.Rule.Source == TraceTickSourceType.Seek && nesting[index].End > index;

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

    public static SKColor GetPlanPartColour(TraceTickTrack track, TraceTick tick, TraceTickDefinition rule)
        => rule.PlanPartColour is { } colour ? ParseColour(colour) : track.GetColour(tick) ?? DefaultColour;

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
        => GetText(step.Track, step.Tick, PredicateTagRegex().Replace(GetDescriptionTemplate(step), string.Empty)) ?? string.Empty;

    public static PredicateText? GetStepPredicate(TraceTrackStep step)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        foreach (Match match in PredicateTagRegex().Matches(GetDescriptionTemplate(step)))
        {
            if (step.Track.GetPredicate(step.Tick, match.Groups[1].Value) is not { IsEmpty: false } predicate)
            {
                continue;
            }

            if (tokens.Count > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Space, Environment.NewLine));
            }

            tokens.AddRange(predicate.Tokens);
        }

        return tokens.Count > 0 ? new PredicateText(tokens.ToImmutable()) : null;
    }

    public static PredicateText? GetSeekKeyText(List<(string Column, SqlDbType? Type)> columns, List<VariantLocator> keys)
    {
        if (keys.Count == 0 || keys.Count != columns.Count)
        {
            return null;
        }

        var values = ImmutableArray.CreateBuilder<AccessValue>(keys.Count);

        for (var index = 0; index < keys.Count; index++)
        {
            var (column, type) = columns[index];

            if (keys[index].Status != VariantStatus.Value
                || type is not { } dataType
                || GetInteger(dataType, keys[index].Value) is not { } value)
            {
                return null;
            }

            values.Add(AccessValue.FromInteger(dataType, value).WithColumnName(column));
        }

        return PredicateText.From(SeekBounds.Equality(new AccessKey(values.ToImmutable())));
    }

    public static string GetStepDetails(TraceTrackStep step)
        => step.IsReturn ? string.Empty : GetText(step.Track, step.Tick, step.Rule.Details) ?? string.Empty;

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

    private static bool IsNestingStep(TraceTrackStep step)
        => IsSeekSpan(step)
           || (step.Rule.Source != TraceTickSourceType.Open && !string.IsNullOrEmpty(step.Rule.ReturnDescription));

    private static bool IsSeekSpan(TraceTrackStep step)
        => step.Rule.Source == TraceTickSourceType.Seek && step.Tick.Span.To.Position > step.Tick.Span.From.Position;

    private static void RemoveEndedSeeks(List<TraceTrackStep> steps, List<int> calls, TraceTrackStep step)
    {
        while (calls.Count > 0
               && steps[calls[^1]].Rule.Source == TraceTickSourceType.Seek
               && steps[calls[^1]].Tick.Span.To.Position < step.Point.Position)
        {
            calls.RemoveAt(calls.Count - 1);
        }
    }

    private static bool IsSameTick(TraceTrackStep left, TraceTrackStep right)
        => ReferenceEquals(left.Track, right.Track) && left.Tick.Span.From.Position == right.Tick.Span.From.Position;

    private static bool IsForwardingCall(TraceTick tick, TraceTickDefinition rule)
        => rule.Source is TraceTickSourceType.RowReturn or TraceTickSourceType.Open && OperatorClassifier.IsComputeScalar(tick.Operator);

    private static string? GetPlaceholder(TraceResult trace,
                                          TraceTick tick,
                                          string name,
                                          Func<TraceTick, string> getParentName,
                                          Func<TraceTick, string, PredicateText?> getPredicate)
        => name switch
        {
            _ when IsPlaceholder(name, TraceTickDefinition.OperatorNamePlaceholder) => tick.Operator.OperatorDescription,
            _ when IsPlaceholder(name, TraceTickDefinition.ParentOperatorNamePlaceholder) => getParentName(tick),
            _ when IsPredicatePlaceholder(name) => getPredicate(tick, name)?.ToString() ?? string.Empty,
            _ when IsPlaceholder(name, TraceTickDefinition.FunctionPlaceholder) => tick.Node.Symbol,
            _ when IsPlaceholder(name, TraceTickDefinition.RowCallPlaceholder)
                => trace.RowReturnMarks.IsBatch(tick.Operator) ? BatchReturnsTitle : RowReturnsTitle,
            _ when IsPlaceholder(name, TraceTickDefinition.RowUnitPlaceholder)
                => trace.RowReturnMarks.IsBatch(tick.Operator) ? BatchUnit : RowUnit,
            _ when IsPlaceholder(name, TraceTickDefinition.PagePlaceholder)
                => GetPage(trace, tick) is { } page ? GetPageLink(page, GetSlot(trace, tick)) : UnknownValue,
            _ when IsPlaceholder(name, TraceTickDefinition.SlotPlaceholder)
                => GetSlot(trace, tick)?.ToString(CultureInfo.CurrentCulture) ?? UnknownValue,
            _ when IsPlaceholder(name, TraceTickDefinition.HeaderFieldPlaceholder)
                => trace.PageAccesses.GetHeaderAccess(tick.Span.From.Position) is { } header
                    ? string.Join(TraceTickDefinition.ListSeparator, header.GetFields())
                    : UnknownValue,
            _ when IsPlaceholder(name, TraceTickDefinition.PartitionPlaceholder)
                => trace.HashTables.GetLocation(tick)?.Partition.ToString(CultureInfo.CurrentCulture) ?? UnknownValue,
            _ when IsPlaceholder(name, TraceTickDefinition.BucketPlaceholder)
                => trace.HashTables.GetLocation(tick)?.Bucket.ToString(CultureInfo.CurrentCulture) ?? UnknownValue,
            _ => null
        };

    private static long? GetInteger(SqlDbType type, ulong value) => type switch
    {
        SqlDbType.TinyInt => (byte)value,
        SqlDbType.SmallInt => (short)value,
        SqlDbType.Int => (int)value,
        SqlDbType.BigInt => (long)value,
        _ => null
    };

    private static bool IsPlaceholder(string name, string placeholder) => name.Equals(placeholder, StringComparison.OrdinalIgnoreCase);

    private static bool IsPredicatePlaceholder(string name)
        => IsPlaceholder(name, TraceTickDefinition.ExpressionPlaceholder)
           || name.StartsWith(TraceTickDefinition.ExecutionPlanPrefix, StringComparison.OrdinalIgnoreCase);

    private static string GetDescriptionTemplate(TraceTrackStep step)
        => (step.IsReturn ? step.Rule.ReturnDescription : step.Rule.Description) ?? string.Empty;

    private static PageAddress? GetPage(TraceResult trace, TraceTick tick)
        => trace.PageAccesses.GetRowAccess(tick.Span.From.Position)?.Page
           ?? trace.PageAccesses.GetHeaderAccess(tick.Span.From.Position)?.Access.Page
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

    private static TraceTickTrack WithWatches(TraceTickTrack track,
                                              IReadOnlyList<TraceTickDefinition> rules,
                                              int[] ruleByValue,
                                              Func<TraceTickDefinition, TraceWatch> getWatch)
    {
        string? GetText(TraceTick tick)
            => ruleByValue[tick.Value] is >= 0 and var rule && getWatch(rules[rule]).GetValue(tick) is { } value
                ? FormatWatchValue(rules[rule], value)
                : null;

        return track with
        {
            GetText = GetText,
            GetDetails = t => ruleByValue[t.Value] is >= 0 and var rule && GetText(t) is { } text
                                  ? [$"{rules[rule].GetLabel()} {text}", GetWatchTarget(rules[rule])]
                                  : []
        };
    }

    private static IEnumerable<string> GetDetails(TraceResult trace, TraceTickDefinition rule, TraceTick tick) => rule.Source switch
    {
        TraceTickSourceType.RowAccess => GetRowAccessDetails(tick, trace.PageAccesses, trace.BufferPool),
        TraceTickSourceType.PageHeaderAccess => GetHeaderAccessDetails(tick, trace.PageAccesses),
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

    private static IEnumerable<string> GetHeaderAccessDetails(TraceTick tick, TracePageAccessList accesses)
    {
        if (accesses.GetHeaderAccess(tick.Span.From.Position) is not { } header)
        {
            yield break;
        }

        yield return $"Page {header.Access.Page}";

        foreach (var field in header.GetFields())
        {
            yield return field;
        }
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

    [GeneratedRegex(@"<Predicate>\s*\{([\w.]+)\}\s*</Predicate>")]
    private static partial Regex PredicateTagRegex();
}
