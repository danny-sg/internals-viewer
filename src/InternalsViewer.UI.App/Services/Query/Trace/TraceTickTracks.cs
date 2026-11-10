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
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
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

    private const string HashLinkScheme = "hash";

    private const string PredicateOpenTag = "<Predicate>";

    private const string PredicateCloseTag = "</Predicate>";

    private const string ResultToken = "{" + TraceTrackRule.ResultPlaceholder + "}";

    private static readonly SKColor PassedUpColour = SKColor.Parse(SymbolCategory.QueryOperator.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor BatchColour = SKColor.Parse(SymbolCategory.ColumnStore.GetCategoryMetadata()!.ForegroundColor);

    public static SKColor DefaultColour => PassedUpColour;

    public static TraceTickTrack Create(TraceResult trace,
                                        IReadOnlyList<TraceTrackRule> rules,
                                        TraceTickTrackSeries series,
                                        int ordinal,
                                        Func<TraceTrackRule, bool> isAdded,
                                        Func<TraceTick, string> getParentName,
                                        Func<TraceTick, string, PredicateText?> getPredicate,
                                        Func<TraceTrackRule, TraceWatch> getWatch)
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
            GetPlaceholder = (t, name) => GetPlaceholder(trace,
                                                         t,
                                                         name,
                                                         getParentName,
                                                         getPredicate,
                                                         ruleByValue[t.Value] is >= 0 and var rule ? rules[rule].GetLabel() : null),
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
            TraceTickSourceType.RowReturn => WithRowReturns(track, trace.SourceTracks.RowReturns, rules, colours, ruleByValue),
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

    public static string GetWatchTarget(TraceTrackRule definition) => $"{definition.Function} + 0x{definition.Offset:X}";

    public static string FormatWatchValue(TraceTrackRule definition, byte[] value)
        => MemoryWatchRowViewModel.GetValue(new HexWatch(0, value.Length, definition.DataType), value);

    public static string? GetText(TraceTickTrack track, TraceTick tick, string? text)
        => string.IsNullOrEmpty(text)
            ? null
            : TraceTrackRule.Format(text, name => track.GetPlaceholder(tick, name))
                                 .Replace(PredicateOpenTag, string.Empty)
                                 .Replace(PredicateCloseTag, string.Empty)
                                 .TrimEnd();

    public static List<TraceTrackStep> GetSteps(IEnumerable<TraceTickTrack> tracks, IReadOnlySet<string>? hidden)
        => GetRuleSteps(tracks.Where(t => hidden?.Contains(t.Name) != true), static _ => true);

    public static List<TraceTrackStep> GetPlanSteps(IEnumerable<TraceTickTrack> tracks, IReadOnlySet<string> additional)
        => GetRuleSteps(tracks, r => IsPlanStep(r, additional));

    public static bool IsPlanStep(TraceTrackRule rule, IReadOnlySet<string> additional)
        => rule.IsPlanDefault || additional.Contains(rule.GetPlanStepKey());

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

    public static SKColor GetPlanPartColour(TraceTickTrack track, TraceTick tick, TraceTrackRule rule)
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

    public static Locator? GetRowId(TraceResult trace, TraceTick tick)
        => tick.Node.Frame is not null
           && trace.CallStore.GetCallList(tick.FunctionAddress, tick.InstanceAddress) is { } list
           && tick.CallIndex < list.Count
            ? list[tick.CallIndex].Locators.Where(l => l.ValueType == LocatorValueType.Row).Select(l => (Locator?)l).FirstOrDefault()
            : null;

    public static PredicateText GetRowIdText(Locator rowId)
        => new([new PredicateToken(PredicateTokenType.Column, "RID"),
                new PredicateToken(PredicateTokenType.Space, " "),
                new PredicateToken(PredicateTokenType.Operator, "="),
                new PredicateToken(PredicateTokenType.Space, " "),
                new PredicateToken(PredicateTokenType.Literal, rowId.ToString())]);

    public static string GetStepDetails(TraceTrackStep step)
        => step.IsReturn ? string.Empty : GetText(step.Track, step.Tick, step.Rule.Details) ?? string.Empty;

    public static string? GetStepCodeColour(TraceTrackStep step)
        => step.Rule.Colour != TraceTrackRule.CategoryColour
           && GetDescriptionTemplate(step).Contains(ResultToken, StringComparison.OrdinalIgnoreCase)
            ? step.Rule.Colour
            : step.Tick.Node.CategoryColour;

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

    public static string GetHashLink(ulong group, int partition, int bucket)
        => string.Create(CultureInfo.InvariantCulture, $"[{bucket}]({HashLinkScheme}:{group:X}:{partition}:{bucket})");

    public static bool TryGetHashLink(Uri? uri, out ulong group, out int partition, out int bucket)
    {
        group = 0;

        partition = 0;

        bucket = 0;

        if (uri is null || !uri.Scheme.Equals(HashLinkScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.OriginalString[(HashLinkScheme.Length + 1)..].Split(':');

        return parts.Length == 3
               && ulong.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out group)
               && int.TryParse(parts[1], CultureInfo.InvariantCulture, out partition)
               && int.TryParse(parts[2], CultureInfo.InvariantCulture, out bucket);
    }

    public static SKColor ParseColour(string colour) => SKColor.TryParse(colour, out var parsed) ? parsed : DefaultColour;

    public static SKColor GetCategoryColour(CallTreeNode node) => node.CategoryColour is { } hex ? ParseColour(hex) : DefaultColour;

    private static SKColor GetColour(TraceTrackRule rule,
                                     SKColor colour,
                                     TraceTick tick,
                                     Dictionary<CallTreeNode, SKColor> categoryColours)
    {
        if (rule.Colour != TraceTrackRule.CategoryColour)
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

    private static bool IsForwardingCall(TraceTick tick, TraceTrackRule rule)
        => rule.Source is TraceTickSourceType.RowReturn or TraceTickSourceType.Open && OperatorClassifier.IsComputeScalar(tick.Operator);

    private static string? GetPlaceholder(TraceResult trace,
                                          TraceTick tick,
                                          string name,
                                          Func<TraceTick, string> getParentName,
                                          Func<TraceTick, string, PredicateText?> getPredicate,
                                          string? result)
        => name switch
        {
            _ when IsPlaceholder(name, TraceTrackRule.OperatorNamePlaceholder) => tick.Operator.OperatorDescription,
            _ when IsPlaceholder(name, TraceTrackRule.ParentOperatorNamePlaceholder) => getParentName(tick),
            _ when IsPredicatePlaceholder(name) => getPredicate(tick, name)?.ToString() ?? string.Empty,
            _ when IsPlaceholder(name, TraceTrackRule.FunctionPlaceholder) => tick.Node.Symbol,
            _ when IsPlaceholder(name, TraceTrackRule.RowCallPlaceholder)
                => trace.SourceTracks.RowReturns.IsBatch(tick.Operator) ? BatchReturnsTitle : RowReturnsTitle,
            _ when IsPlaceholder(name, TraceTrackRule.RowUnitPlaceholder)
                => trace.SourceTracks.RowReturns.IsBatch(tick.Operator) ? BatchUnit : RowUnit,
            _ when IsPlaceholder(name, TraceTrackRule.PagePlaceholder)
                => GetPage(trace, tick) is { } page ? GetPageLink(page, GetSlot(trace, tick)) : UnknownValue,
            _ when IsPlaceholder(name, TraceTrackRule.SlotPlaceholder)
                => GetSlot(trace, tick)?.ToString(CultureInfo.CurrentCulture) ?? UnknownValue,
            _ when IsPlaceholder(name, TraceTrackRule.HeaderFieldPlaceholder)
                => trace.PageAccesses.GetHeaderAccess(tick.Span.From.Position) is { } header
                    ? string.Join(TraceTrackRule.ListSeparator, header.GetFields())
                    : UnknownValue,
            _ when IsPlaceholder(name, TraceTrackRule.ResultPlaceholder) => result,
            _ when IsPlaceholder(name, TraceTrackRule.PartitionPlaceholder)
                => trace.HashTables.GetLocation(tick)?.Partition.ToString(CultureInfo.CurrentCulture) ?? UnknownValue,
            _ when IsPlaceholder(name, TraceTrackRule.BucketPlaceholder)
                => trace.HashTables.GetLocation(tick) is { } location
                    ? GetHashLink(location.Group, location.Partition, location.Bucket)
                    : UnknownValue,
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
        => IsPlaceholder(name, TraceTrackRule.ExpressionPlaceholder)
           || name.StartsWith(TraceTrackRule.ExecutionPlanPrefix, StringComparison.OrdinalIgnoreCase);

    private static string GetDescriptionTemplate(TraceTrackStep step)
        => (step.IsReturn ? step.Rule.ReturnDescription : step.Rule.Description) ?? string.Empty;

    private static PageAddress? GetPage(TraceResult trace, TraceTick tick)
        => trace.PageAccesses.GetRowAccess(tick.Span.From.Position)?.Page
           ?? trace.PageAccesses.GetHeaderAccess(tick.Span.From.Position)?.Access.Page
           ?? trace.PageAccesses.GetFetch(tick.Span.From.Position)?.Page
           ?? GetRead(tick, trace.CallStore)?.Page
           ?? GetRowId(trace, tick)?.Page;

    private static int? GetSlot(TraceResult trace, TraceTick tick)
        => trace.PageAccesses.GetRowAccess(tick.Span.From.Position) is { Offset: { } offset } access
            ? trace.BufferPool.GetLayout(access.Page, access.DatabaseId)?.GetSlot(offset)
            : GetRowId(trace, tick)?.Slot;

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
                                                 IReadOnlyList<TraceTrackRule> rules,
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
                                              IReadOnlyList<TraceTrackRule> rules,
                                              int[] ruleByValue,
                                              Func<TraceTrackRule, TraceWatch> getWatch)
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

    private static IEnumerable<string> GetDetails(TraceResult trace, TraceTrackRule rule, TraceTick tick) => rule.Source switch
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

    private static List<TraceTrackStep> GetRuleSteps(IEnumerable<TraceTickTrack> tracks, Func<TraceTrackRule, bool> isIncluded)
    {
        var steps = new List<TraceTrackStep>();

        foreach (var track in tracks)
        {
            if (track.GetText is not null)
            {
                continue;
            }

            foreach (var tick in track.Series.GetTicks(static _ => true))
            {
                if (track.GetRule(tick) is not { } rule || !isIncluded(rule) || IsForwardingCall(tick, rule))
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
