using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;

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

    void OpenIteratorTab(int block, ulong address, TracePosition position, IReadOnlyList<string> tabs);

    void OpenReferenceGraph(int block, TracePosition position);

    void OpenHashTable(ulong group, TraceHashPart? part);

    void OpenSortTable(ulong iterator, TraceSortPart? part);

    void HighlightMemory(int? block);
}
