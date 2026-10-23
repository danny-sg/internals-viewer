using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public interface IPlayheadChart
{
    void ZoomTo(TraceSpan span);

    void ZoomToCall(CallStackNode node, int callIndex);

    void ScopeTo(CallStackNode node);

    void SelectAdjacentCall(bool forward);

    void MarkCall(CallStackNode? node, int callIndex);

    void MarkPosition(TracePoint? point);

    void HighlightCalls(IReadOnlyCollection<CallStackNode>? nodes);

    void MovePlayheadTo(CallStackNode node, int callIndex);
}
