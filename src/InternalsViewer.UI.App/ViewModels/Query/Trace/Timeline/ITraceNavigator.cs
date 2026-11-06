using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public interface ITraceNavigator
{
    void MovePlayhead(TracePoint point);

    void ZoomToCall(CallTreeNode node, int callIndex);

    void ShowCallStack(TracePoint point);

    void FindInCallTree(CallTreeNode node, int callIndex);

    void FindReferences(ulong value);

    void Search(string text);

    void OpenBufferPage(TraceBufferReference reference);

    void OpenMemory(int block, ulong address, TracePosition position, int length = 1);

    void OpenReferenceGraph(int block, TracePosition position);

    void HighlightMemory(int? block);
}
