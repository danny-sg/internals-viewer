using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public interface IPlayheadChart
{
    void ZoomTo(TraceSpan span);

    void ZoomToCall(CallTreeNode node, int callIndex);

    void ScopeTo(CallTreeNode node);

    void SelectAdjacentCall(bool forward);

    void MarkPositions(TracePoint[]? points);

    void ShowMemoryUpdates(TraceMemoryWrites? writes, string? name);

    TraceViewRange? GetViewRange();
}
