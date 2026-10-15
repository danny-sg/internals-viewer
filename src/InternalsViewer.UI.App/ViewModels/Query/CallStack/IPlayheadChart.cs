using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public interface IPlayheadChart
{
    void ZoomTo(double start, double end);

    void ZoomToCall(CallStackNode node, int call);

    void ScopeTo(CallStackNode node);

    void StepPlayhead(bool forward);

    void MarkCall(CallStackNode? node, int call);

    void MovePlayheadTo(CallStackNode node, int call);

    void OpenCallStack(CallStackNode node, int call);
}
