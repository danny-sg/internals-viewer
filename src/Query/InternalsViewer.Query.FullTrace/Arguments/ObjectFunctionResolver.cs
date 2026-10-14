using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Arguments;

internal static class ObjectFunctionResolver
{
    private static readonly string[] DestructorPrefixes = ["??1", "??_G", "??_E", "??_D"];

    public static ObjectFunctions Resolve(IEnumerable<CallStackNode?> nodes, string symbolsPath)
    {
        using var resolver = new CallstackResolver(symbolsPath);

        var members = new Dictionary<ulong, TraceMember>();

        var allocators = new HashSet<ulong>();

        var frees = new Dictionary<ulong, int>();

        var seen = new HashSet<ulong>();

        foreach (var node in nodes)
        {
            if (node?.Frame is not { Address: not 0, Resolved: { } resolved } frame || !seen.Add(frame.Address))
            {
                continue;
            }

            if (GetMemoryFunction(frame.Address, resolved) is { IsVirtual: false } memory)
            {
                if (memory.Allocates)
                {
                    allocators.Add(frame.Address);
                }
                else if (memory is { Operation: MemoryOperation.Free, PointerSlot: >= 0 })
                {
                    frees[frame.Address] = memory.PointerSlot;
                }
            }

            if (resolved.ClassName is not { Length: > 0 } className
                || resolver.ResolveDecoratedName(frame) is not { } decoratedName
                || FunctionSignature.KindOf(decoratedName) != FunctionKind.Member)
            {
                continue;
            }

            var kind = GetKind(decoratedName);

            if (kind != TraceMemberKind.Method && resolver.IsFolded(frame))
            {
                kind = TraceMemberKind.Method;
            }

            members[frame.Address] = new TraceMember(className, kind);
        }

        return new ObjectFunctions(members, allocators, frees);
    }

    internal static TraceMemberKind GetKind(string decoratedName)
    {
        if (decoratedName.StartsWith("??0", StringComparison.Ordinal))
        {
            return TraceMemberKind.Constructor;
        }

        return DestructorPrefixes.Any(p => decoratedName.StartsWith(p, StringComparison.Ordinal))
            ? TraceMemberKind.Destructor
            : TraceMemberKind.Method;
    }

    private static MemoryFunction? GetMemoryFunction(ulong address, ResolvedCallstackFrame resolved)
    {
        var symbol = resolved.ClassName is { Length: > 0 } className ? $"{className}::{resolved.MethodName}" : resolved.MethodName;

        return MemoryFunction.Classify(address, symbol) ?? MemoryFunction.ClassifyExport(address, resolved.MethodName);
    }
}
