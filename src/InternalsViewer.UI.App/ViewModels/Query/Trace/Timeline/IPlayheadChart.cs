using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public interface IPlayheadChart
{
    void ZoomTo(TraceSpan span);

    void ZoomToCall(CallStackNode node, int callIndex);

    void ScopeTo(CallStackNode node);

    void SelectAdjacentCall(bool forward);

    void MarkCall(CallStackNode? node, int callIndex);

    void MarkPositions(TracePoint[]? points);

    void MovePlayheadTo(CallStackNode node, int callIndex);

    void ShowMemoryUpdates(TraceMemoryWrites? writes, string? name);

    TraceViewRange? GetViewRange();
}
