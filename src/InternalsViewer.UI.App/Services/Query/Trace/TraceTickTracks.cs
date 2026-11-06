using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using SkiaSharp;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.UI.App.Services.Query.Trace;

public static class TraceTickTracks
{
    public const int CustomOrdinal = 4;

    public static readonly SKColor LightBackground = new(0xFF, 0xFF, 0xFF, 0x40);

    private const string GetRowsName = "GetRow";

    private const string BatchReturnsTitle = "GetBatch";

    private const string GetRowsMenuText = "GetRow/GetBatch";

    private const string ReadsName = "Reads";

    private const string EvaluationsName = "Evaluations";

    private const string RowAccessName = "Row Access";

    private const string SeekName = "Seek";

    private const string CallLabel = "Call";

    private static readonly SKColor PassedUpColour = SKColor.Parse(SymbolCategory.QueryOperator.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor BatchColour = SKColor.Parse(SymbolCategory.ColumnStore.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor ReadColour = SKColor.Parse(SymbolCategory.BufferPool.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor RowAccessColour = new(0x8C, 0xC4, 0xFF);

    private static readonly SKColor SeekColour = SKColor.Parse(SymbolCategory.BTreeNavigation.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor FalseColour = new(0xF0, 0x48, 0x48);

    private static readonly SKColor TrueColour = new(0x5C, 0xD6, 0x6B);

    private static readonly SKColor NullColour = new(0x9E, 0x9E, 0x9E);

    private static readonly Dictionary<RowReturnType, SKColor> ReturnColours = new()
    {
        [RowReturnType.Skipped] = new SKColor(0xC8, 0xC8, 0xC8),
        [RowReturnType.Rejected] = new SKColor(0xF0, 0x48, 0x48),
        [RowReturnType.Returned] = PassedUpColour,
        [RowReturnType.FakeRow] = new SKColor(0xFF, 0xC8, 0x57),
        [RowReturnType.Other] = new SKColor(0xC0, 0x84, 0xFC),
        [RowReturnType.End] = new SKColor(0xF0, 0xF0, 0xF0)
    };

    public static TraceTickTrack GetRows(TraceRowReturnMarkMap marks)
    {
        return new(GetRowsName, TraceTickLocation.Top, 0, marks.Ticks)
        {
            MenuText = GetRowsMenuText,
            Values =
        [
            Value(RowReturnType.Skipped, "Skipped"),
            Value(RowReturnType.Rejected, "Rejected"),
            Value(RowReturnType.Returned, "Returned"),
            Value(RowReturnType.FakeRow, "Fake Row"),
            Value(RowReturnType.Other, "Other Status"),
            Value(RowReturnType.End, "End")
        ],
            GetColour = t => (RowReturnType)t.Value == RowReturnType.Returned
                                 ? GetPassedUpColour(marks, t.Operator)
                                 : ReturnColours.GetValueOrDefault((RowReturnType)t.Value, PassedUpColour),
            Colour = PassedUpColour,
            GetTrackColour = o => GetPassedUpColour(marks, o),
            HasCallReturnLine = true,
            IsStatementTrack = true,
            TickWidth = 2f,
            GetTitle = o => marks.IsBatch(o) ? BatchReturnsTitle : GetRowsName
        };
    }

    public static TraceTickTrack Reads(TraceTickSeries series, TraceCallStore calls)
    {
        return new(ReadsName, TraceTickLocation.Bottom, 0, series)
        {
            Values = [Value(TraceReadType.Page, "Page Read"), Value(TraceReadType.ReadAhead, "Read Ahead")],
            GetColour = _ => ReadColour,
            Colour = ReadColour,
            GetDetails = t => GetReadDetails(t, calls)
        };
    }

    public static TraceTickTrack Evaluations(TraceTickSeries series) => new(EvaluationsName, TraceTickLocation.Bottom, 1, series)
    {
        Values =
        [
            Value(TraceEvaluationResult.False, "False"),
            Value(TraceEvaluationResult.Unknown, "NULL"),
            Value(TraceEvaluationResult.True, "True")
        ],
        GetColour = t => (TraceEvaluationResult)t.Value switch
        {
            TraceEvaluationResult.True => TrueColour,
            TraceEvaluationResult.Unknown => NullColour,
            _ => FalseColour
        }
    };

    public static TraceTickTrack RowAccesses(TraceTickSeries series, TracePageAccessList accesses, TraceBufferPool pool)
        => new(RowAccessName, TraceTickLocation.Bottom, 2, series)
        {
            Values = [new TraceTickValue(0, RowAccessName)],
            GetColour = _ => RowAccessColour,
            Colour = RowAccessColour,
            GetDetails = t => GetRowAccessDetails(t, accesses, pool)
        };

    public static TraceTickTrack Seeks(TraceTickSeries series, TracePageAccessList accesses)
        => new(SeekName, TraceTickLocation.Bottom, 3, series)
        {
            Values =
            [
                Value(TraceSeekMarkType.Seek, SeekName),
                Value(TraceSeekMarkType.Root, "Root Page"),
                Value(TraceSeekMarkType.Intermediate, "Intermediate Page"),
                Value(TraceSeekMarkType.Leaf, "Leaf Page")
            ],
            GetColour = t => (TraceSeekMarkType)t.Value == TraceSeekMarkType.Seek ? SeekColour : ReadColour,
            Colour = SeekColour,
            TickWidth = 2f,
            GetDetails = t => GetSeekDetails(t, accesses)
        };

    public static SKColor DefaultColour => PassedUpColour;

    public static TraceTickTrack Custom(TraceTickDefinition definition, TraceTickSeries series)
    {
        var colour = ParseColour(definition.Colour);

        return new TraceTickTrack(definition.Name, TraceTickLocation.Bottom, CustomOrdinal, series)
        {
            Values = [new TraceTickValue(0, CallLabel)],
            GetColour = _ => colour,
            Colour = colour,
            Background = LightBackground,
            HeightFactor = (float)definition.HeightFactor,
            WidthFactor = (float)definition.WidthFactor,
            Definition = definition
        };
    }

    public static SKColor ParseColour(string colour) => SKColor.TryParse(colour, out var parsed) ? parsed : DefaultColour;

    public static SKColor GetCategoryColour(CallTreeNode node) => node.CategoryColour is { } hex ? ParseColour(hex) : DefaultColour;

    private static SKColor GetPassedUpColour(TraceRowReturnMarkMap marks, ExecutionOperatorEvent operatorEvent)
        => marks.IsBatch(operatorEvent) ? BatchColour : PassedUpColour;

    private static TraceTickValue Value<T>(T value, string label) where T : System.Enum
        => new(System.Convert.ToByte(value), label);

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
        if (calls.GetCallList(tick.FunctionAddress, tick.InstanceAddress) is not { } list
            || tick.CallIndex >= list.Count
            || TraceReadResolver.GetRead(list[tick.CallIndex], (TraceReadType)tick.Value) is not { } read)
        {
            yield break;
        }

        yield return (TraceReadType)tick.Value == TraceReadType.ReadAhead
            ? $"{read.PageCount:N0} {(read.PageCount == 1 ? "Page" : "Pages")} From {read.Page}"
            : $"Page {read.Page}";
    }
}
