using System.Collections.Generic;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public static class MemoryChangeNavigation
{
    public static int GetNextChange(IReadOnlyList<MemoryHistoryWrite> writes, int offset, TracePosition position)
    {
        for (var index = 0; index < writes.Count; index++)
        {
            if (IsChange(writes[index], offset) && writes[index].Position > position)
            {
                return index;
            }
        }

        return -1;
    }

    public static int GetPreviousChange(IReadOnlyList<MemoryHistoryWrite> writes, int offset, TracePosition position)
    {
        for (var index = writes.Count - 1; index >= 0; index--)
        {
            if (IsChange(writes[index], offset) && writes[index].Position < position)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsChange(MemoryHistoryWrite write, int offset)
        => write.Type == MemoryHistoryChangeType.Changed && offset >= write.Offset && offset < write.Offset + write.Length;
}
