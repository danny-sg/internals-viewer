using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory.Enums;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public static class MemoryChangeRuns
{
    public static List<MemoryChangeRun> GetRuns(IReadOnlyList<MemoryHistoryWrite> writes, int count, int size)
    {
        var states = new MemoryChangeState[size];

        var writers = new int[size];

        for (var index = 0; index < writes.Count; index++)
        {
            var write = writes[index];

            if (write.Type != MemoryHistoryChangeType.Changed)
            {
                continue;
            }

            var end = Math.Min(write.Offset + write.Length, size);

            for (var offset = Math.Max(write.Offset, 0); offset < end; offset++)
            {
                if (index < count)
                {
                    states[offset] = MemoryChangeState.Changed;

                    writers[offset] = write.CallNodeIndex;
                }
                else if (states[offset] == MemoryChangeState.Unchanged)
                {
                    states[offset] = MemoryChangeState.Pending;
                }
            }
        }

        var runs = new List<MemoryChangeRun>();

        var start = 0;

        for (var offset = 1; offset <= size; offset++)
        {
            if (offset < size && states[offset] == states[start] && writers[offset] == writers[start])
            {
                continue;
            }

            if (states[start] != MemoryChangeState.Unchanged)
            {
                runs.Add(new MemoryChangeRun(start, offset - 1, states[start], writers[start]));
            }

            start = offset;
        }

        return runs;
    }
}

public readonly record struct MemoryChangeRun(int Start, int End, MemoryChangeState State, int CallNodeIndex);
