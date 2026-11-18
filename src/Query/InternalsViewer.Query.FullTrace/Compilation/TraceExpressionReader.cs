using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Compilation;

public static class TraceExpressionReader
{
    private const int MaximumDepth = 64;

    private const int MaximumNodes = 2000;

    private const int MaximumChildren = 64;

    private const int PointerSize = sizeof(ulong);

    public static TraceExpressionNode? Read(ITraceMemoryReader memory, Func<ulong, string?> getClassName, ulong root)
        => Read(memory, getClassName, root, [], 0);

    private static TraceExpressionNode? Read(ITraceMemoryReader memory,
                                             Func<ulong, string?> getClassName,
                                             ulong address,
                                             HashSet<ulong> visited,
                                             int depth)
    {
        var layout = memory.Layout.OptimizerExpression;

        if (address == 0
            || depth > MaximumDepth
            || visited.Count >= MaximumNodes
            || !visited.Add(address)
            || !memory.TryRead(address + (ulong)layout.OperatorField.Offset, out ulong operatorAddress)
            || !memory.TryRead(address + (ulong)layout.ChildCountField.Offset, out int count)
            || !memory.TryRead(address + (ulong)layout.ChildrenField.Offset, out ulong children))
        {
            return null;
        }

        var node = new TraceExpressionNode(address, operatorAddress, getClassName(operatorAddress), []);

        for (var index = 0; children != 0 && index < Math.Min(count, MaximumChildren); index++)
        {
            if (memory.TryRead(children + (ulong)(index * PointerSize + layout.ChildField.Offset), out ulong child)
                && Read(memory, getClassName, child, visited, depth + 1) is { } item)
            {
                node.Children.Add(item);
            }
        }

        return node;
    }
}
