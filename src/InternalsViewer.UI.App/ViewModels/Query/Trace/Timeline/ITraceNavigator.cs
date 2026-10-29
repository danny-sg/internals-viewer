using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public interface ITraceNavigator
{
    void MovePlayhead(TracePoint point);

    void ZoomToCall(CallStackNode node, int callIndex);

    void ShowCallStack(TracePoint point);

    void FindInCallTree(CallStackNode node, int callIndex);

    void FindReferences(ulong value);

    void Search(string text);

    void OpenBufferPage(TraceBufferReference reference);
}
