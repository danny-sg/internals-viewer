using InternalsViewer.Query.CallStack.TimeTravel.Native;

namespace InternalsViewer.Query.CallStack.TimeTravel;

/// <summary>
/// Builds a call tree from a list of time travel call nodes and modules
/// </summary>
/// <remarks>
/// Additionally returns a map from the original native call node index to the corresponding call stack node in the tree
/// </remarks>
internal static class TimeTravelCallTreeBuilder
{
    public static (CallStackTree Tree, CallStackNode[] Nodes) Build(IReadOnlyList<TimeTravelCallNode> calls,
                                                                    IReadOnlyList<TimeTravelModule> modules)
    {
        var callStack = new CallStackTree { ActivityFromTrace = true };

        var ordered = modules.OrderBy(m => m.StartAddress).ToArray();

        ulong[] starts = [.. ordered.Select(m => m.StartAddress)];

        var nodes = new CallStackNode[calls.Count];

        for (var i = 0; i < calls.Count; i++)
        {
            var call = calls[i];

            var parent = call.Parent < 0 ? callStack.Root : nodes[call.Parent];

            nodes[i] = callStack.AddCall(parent, CreateFrame(ordered, starts, call), (long)call.Calls);
        }

        return (callStack, nodes);
    }

    private static CallstackFrame CreateFrame(TimeTravelModule[] modules, ulong[] starts, TimeTravelCallNode call)
    {
        var module = Find(modules, starts, call.Address);

        if (module is null)
        {
            return new CallstackFrame { Module = $"Unknown@0x{call.Address:X}", Address = call.Address, Instance = call.Instance };
        }

        return module.Frame(call.Address, call.Instance);
    }

    /// <summary>
    /// Find a module from an address in a list of modules + start addresses
    /// </summary>
    private static TimeTravelModule? Find(TimeTravelModule[] modules, ulong[] starts, ulong address)
    {
        var index = Array.BinarySearch(starts, address);

        index = index >= 0 ? index : ~index - 1;

        return index >= 0 && address < modules[index].StartAddress + modules[index].Size ? modules[index] : null;
    }
}
