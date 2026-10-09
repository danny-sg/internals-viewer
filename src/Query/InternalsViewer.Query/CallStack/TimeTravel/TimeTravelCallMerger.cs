using InternalsViewer.Query.CallStack.TimeTravel.Native;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class TimeTravelCallMerger
{
    public static CallStackNode[] Merge(CallStackTree callStack, TimeTravelCallTree calls, int activityBuckets)
    {
        var modules = calls.Modules.OrderBy(m => m.Address).Select(TimeTravelModuleIdentity.Describe).ToArray();

        var nodes = new CallStackNode[calls.Nodes.Length];

        for (var i = 0; i < calls.Nodes.Length; i++)
        {
            var call = calls.Nodes[i];

            var parent = call.Parent < 0 ? callStack.Root : nodes[call.Parent];

            nodes[i] = callStack.AddCall(parent, CreateFrame(modules, call), (long)call.Calls);
        }

        AddActivity(nodes, calls.Activity, activityBuckets);

        callStack.ActivityFromTrace = true;

        return nodes;
    }

    private static void AddActivity(CallStackNode[] nodes, TimeTravelCallActivity[] activity, int buckets)
    {
        if (activity.Length == 0 || buckets <= 0)
        {
            return;
        }

        var first = activity.Min(a => a.Slice);

        var span = activity.Max(a => a.Slice) - first + 1;

        foreach (var run in activity)
        {
            var node = nodes[run.Node];

            if (node.CallActivity.Length != buckets)
            {
                node.CallActivity = new int[buckets];
            }

            node.CallActivity[(run.Slice - first) * buckets / span] += (int)run.Calls;
        }
    }

    private static CallstackFrame CreateFrame(TimeTravelModuleIdentity[] modules, TimeTravelCallNode call)
    {
        var module = Find(modules, call.Address);

        if (module is null)
        {
            return new CallstackFrame { Module = $"Unknown@0x{call.Address:X}", Address = call.Address, Instance = call.Instance };
        }

        return module.Frame(call.Address, call.Instance);
    }

    private static TimeTravelModuleIdentity? Find(TimeTravelModuleIdentity[] modules, ulong address)
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
