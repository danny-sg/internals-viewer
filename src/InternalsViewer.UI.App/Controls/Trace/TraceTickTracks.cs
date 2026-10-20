using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Trace.Enums;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

public static class TraceTickTracks
{
    public const int CustomOrdinal = 2;

    private const string GetRowsName = "GetRow Returns";

    private const string BatchReturnsTitle = "GetBatch Returns";

    private const string ReadsName = "Reads";

    private const string EvaluationsName = "Evaluations";

    private const string CallLabel = "Call";

    private static readonly SKColor PassedUpColour = SKColor.Parse(SymbolCategory.QueryOperator.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor FalseColour = new(0xF0, 0x48, 0x48);

    private static readonly SKColor TrueColour = new(0x5C, 0xD6, 0x6B);

    private static readonly SKColor NullColour = new(0x9E, 0x9E, 0x9E);

    private static readonly SKColor CustomBackground = new(0xFF, 0xFF, 0xFF, 0x40);

    private static readonly Dictionary<RowReturnType, SKColor> ReturnColours = new()
    {
        [RowReturnType.NotPassedUp] = new SKColor(0x8A, 0x8A, 0x8A),
        [RowReturnType.Stored] = new SKColor(0x4F, 0xC3, 0xF7),
        [RowReturnType.Rejected] = new SKColor(0xF0, 0x48, 0x48),
        [RowReturnType.PassedUp] = PassedUpColour,
        [RowReturnType.FakeRow] = new SKColor(0xFF, 0xC8, 0x57),
        [RowReturnType.Other] = new SKColor(0xC0, 0x84, 0xFC),
        [RowReturnType.End] = new SKColor(0xF0, 0xF0, 0xF0)
    };

    public static TraceTickTrack GetRows(TraceRowReturnMarks marks) => new(GetRowsName, TraceTickLocation.Top, 0, marks.Ticks)
    {
        Values =
        [
            Value(RowReturnType.NotPassedUp, "Not Passed Up"),
            Value(RowReturnType.Stored, "Stored"),
            Value(RowReturnType.Rejected, "Rejected"),
            Value(RowReturnType.PassedUp, "Passed Up"),
            Value(RowReturnType.FakeRow, "Fake Row"),
            Value(RowReturnType.Other, "Other Status"),
            Value(RowReturnType.End, "End")
        ],
        GetColour = t => ReturnColours.GetValueOrDefault((RowReturnType)t.Value, PassedUpColour),
        HasCallReturnLine = true,
        TickWidth = 2f,
        GetTitle = o => marks.IsBatch(o) ? BatchReturnsTitle : GetRowsName
    };

    public static TraceTickTrack Reads(TraceTickSeries series, TraceCallStore calls) => new(ReadsName, TraceTickLocation.Bottom, 0, series)
    {
        Values = [Value(TraceReadType.Page, "Page Read"), Value(TraceReadType.ReadAhead, "Read Ahead")],
        GetDetails = t => GetReadDetails(t, calls)
    };

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

    public static SKColor DefaultColour => PassedUpColour;

    public static TraceTickTrack Custom(TraceTickDefinition definition, TraceTickSeries series)
    {
        var colour = ParseColour(definition.Colour);

        return new TraceTickTrack(definition.Name, TraceTickLocation.Bottom, CustomOrdinal, series)
        {
            Values = [new TraceTickValue(0, CallLabel)],
            GetColour = _ => colour,
            Background = CustomBackground,
            HeightFactor = (float)definition.HeightFactor,
            WidthFactor = (float)definition.WidthFactor,
            Definition = definition
        };
    }

    public static SKColor ParseColour(string colour) => SKColor.TryParse(colour, out var parsed) ? parsed : DefaultColour;

    public static SKColor GetCategoryColour(CallStackNode node) => node.CategoryColour is { } hex ? ParseColour(hex) : DefaultColour;

    private static TraceTickValue Value<T>(T value, string label) where T : System.Enum
        => new(System.Convert.ToByte(value), label);

    private static IEnumerable<string> GetReadDetails(TraceTick tick, TraceCallStore calls)
    {
        if (calls.GetCallList(tick.Function, tick.Instance) is not { } list
            || tick.Index >= list.Count
            || TraceReads.GetRead(list[tick.Index], (TraceReadType)tick.Value) is not { } read)
        {
            yield break;
        }

        yield return (TraceReadType)tick.Value == TraceReadType.ReadAhead
            ? $"{read.PageCount:N0} {(read.PageCount == 1 ? "Page" : "Pages")} From {read.Page}"
            : $"Page {read.Page}";
    }
}
