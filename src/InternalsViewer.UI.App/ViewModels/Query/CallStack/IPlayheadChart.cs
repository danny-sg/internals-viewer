using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public interface IPlayheadChart
{
    void ZoomTo(TraceSpan span);

    void ZoomToCall(CallStackNode node, int callIndex);

    void ScopeTo(CallStackNode node);

    void SelectAdjacentCall(bool forward);

    void MarkCall(CallStackNode? node, int callIndex);

    void MovePlayheadTo(CallStackNode node, int callIndex);
}
