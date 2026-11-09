using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

/// <summary>
/// Builds a call tree from a list of time travel call nodes and modules
/// </summary>
/// <remarks>
/// Additionally returns a map from the original native call node index to the corresponding call stack node in the tree
/// </remarks>
internal static class CallTreeBuilder
{
    public static (CallTree Tree, CallTreeNode[] Nodes) Build(IReadOnlyList<CallNode> calls,
                                                                    IReadOnlyList<TraceModule> modules)
    {
        var callTree = new CallTree();

        var ordered = modules.OrderBy(m => m.StartAddress).ToArray();

        ulong[] starts = [.. ordered.Select(m => m.StartAddress)];

        var nodes = new CallTreeNode[calls.Count];

        for (var i = 0; i < calls.Count; i++)
        {
            var call = calls[i];

            var parent = call.ParentIndex < 0 ? callTree.Root : nodes[call.ParentIndex];

            nodes[i] = callTree.AddCall(parent, CreateFrame(ordered, starts, call), (long)call.CallCount);
        }

        return (callTree, nodes);
    }

    private static CallFrame CreateFrame(TraceModule[] modules, ulong[] starts, CallNode call)
    {
        var module = FindModule(modules, starts, call.FunctionAddress);

        if (module is null)
        {
            return new CallFrame
            {
                Module = new FrameModule($"Unknown@0x{call.FunctionAddress:X}"),
                Address = call.FunctionAddress,
                InstanceAddress = call.InstanceAddress
            };
        }

        return module.Frame(call.FunctionAddress, call.InstanceAddress);
    }

    /// <summary>
    /// Find a module from an address in a list of modules + start addresses
    /// </summary>
    private static TraceModule? FindModule(TraceModule[] modules, ulong[] starts, ulong address)
    {
        var index = Array.BinarySearch(starts, address);

        index = index >= 0 ? index : ~index - 1;

        return index >= 0 && address < modules[index].StartAddress + modules[index].Size ? modules[index] : null;
    }
}
