using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline.Enums;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class TimeTravelCallMerger
{
    public static CallStackNode[] Merge(CallStackTree callStack,
                                        IReadOnlyList<TimeTravelCallNode> calls,
                                        IReadOnlyList<TimeTravelModule> modules)
    {
        var ordered = modules.OrderBy(m => m.Address).ToArray();

        var nodes = new CallStackNode[calls.Count];

        for (var i = 0; i < calls.Count; i++)
        {
            var call = calls[i];

            var parent = call.Parent < 0 ? callStack.Root : nodes[call.Parent];

            nodes[i] = callStack.AddCall(parent, CreateFrame(ordered, call), (long)call.Calls);
        }

        callStack.ActivityFromTrace = true;

        return nodes;
    }

    public static void AddActivity(TimeTravelTimeline timeline, int buckets)
    {
        var start = timeline.StartOf(TimeTravelTimelineAxis.Position);

        var length = timeline.EndOf(TimeTravelTimelineAxis.Position) - start;

        if (buckets <= 0 || length <= 0)
        {
            return;
        }

        foreach (var span in timeline.ResolvedSpans())
        {
            if (span.Row.FlagsAt(span.Index).HasFlag(TimeTravelSpanFlags.StartUnknown))
            {
                continue;
            }

            if (span.Node.CallActivity.Length != buckets)
            {
                span.Node.CallActivity = new int[buckets];
            }

            var bucket = (int)((span.StartOf(TimeTravelTimelineAxis.Position) - start) / length * buckets);

            span.Node.CallActivity[Math.Clamp(bucket, 0, buckets - 1)]++;
        }
    }

    private static CallstackFrame CreateFrame(TimeTravelModule[] modules, TimeTravelCallNode call)
    {
        var module = Find(modules, call.Address);

        if (module is null)
        {
            return new CallstackFrame { Module = $"Unknown@0x{call.Address:X}", Address = call.Address, Instance = call.Instance };
        }

        return module.Frame(call.Address, call.Instance);
    }

    private static TimeTravelModule? Find(TimeTravelModule[] modules, ulong address)
    {
        var low = 0;
        var high = modules.Length - 1;

        while (low <= high)
        {
            var middle = (low + high) / 2;

            var module = modules[middle];

            if (address < module.Address)
            {
                high = middle - 1;
            }
            else if (address >= module.Address + module.Size)
            {
                low = middle + 1;
            }
            else
            {
                return module;
            }
        }

        return null;
    }
}
