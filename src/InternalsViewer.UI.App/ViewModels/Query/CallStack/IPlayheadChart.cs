using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public interface IPlayheadChart
{
    void ZoomTo(TraceSpan span);

    void ZoomToCall(CallStackNode node, int call);

    void ScopeTo(CallStackNode node);

    void SelectAdjacentCall(bool forward);

    void MarkCall(CallStackNode? node, int call);

    void MovePlayheadTo(CallStackNode node, int call);
}
