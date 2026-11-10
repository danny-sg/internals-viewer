using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed class TraceSourceTrackMap(TraceTimeline timeline, TraceCallStore callStore, TraceOperatorLifetimeList lifetimes)
{
    public TraceRowReturnMarkMap RowReturns { get; init; } = TraceRowReturnMarkMap.Empty;

    public TraceTickSeries RowAccesses { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries PageHeaderAccesses { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries PageFetches { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries Seeks { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries Opens => field ??= TraceOperatorOpenTrackBuilder.Build(Timeline, Lifetimes);

    internal EnclosingOperatorCallIndex EnclosingCalls
    {
        get => field ??= EnclosingOperatorCallIndex.Build(CallStore, Lifetimes.OperatorsByInstance);
        init;
    }

    internal EnclosingOperatorCallIndex PullerCalls
        => field ??= EnclosingOperatorCallIndex.Build(CallStore,
                                                      Lifetimes.OperatorsByInstance
                                                               .Where(o => !OperatorClassifier.IsComputeScalar(o.Value))
                                                               .ToDictionary(o => o.Key, o => o.Value));

    private TraceTimeline Timeline { get; } = timeline;

    private TraceCallStore CallStore { get; } = callStore;

    private TraceOperatorLifetimeList Lifetimes { get; } = lifetimes;

    private TraceTickSeries SeekRanges => field ??= TraceSeekRangeTrackBuilder.Build(Timeline, CallStore, Lifetimes, EnclosingCalls);

    private TraceTickSeries SeekKeys => field ??= TraceSeekRangeTrackBuilder.BuildKeys(Timeline, CallStore, Lifetimes, EnclosingCalls);

    public int GetSeekRange(TraceTick tick)
        => TraceSeekRangeTrackBuilder.GetRange(SeekRanges, CallStore, tick.Operator, tick.Thread, tick.Span.From.Position);

    public List<VariantLocator> GetSeekKeys(TraceTick tick)
        => TraceSeekRangeTrackBuilder.GetKeys(SeekKeys, CallStore, tick.Operator, tick.Thread, tick.Span.From.Position);

    internal TraceTickSeries GetSeries(TraceTickSourceType source) => source switch
    {
        TraceTickSourceType.RowReturn => RowReturns.Ticks,
        TraceTickSourceType.RowAccess => RowAccesses,
        TraceTickSourceType.PageHeaderAccess => PageHeaderAccesses,
        TraceTickSourceType.Seek => Seeks,
        TraceTickSourceType.PageFetch => PageFetches,
        TraceTickSourceType.Open => Opens,
        _ => TraceTickSeries.Empty
    };
}
