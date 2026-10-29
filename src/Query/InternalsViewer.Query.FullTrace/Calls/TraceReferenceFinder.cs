using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceReferenceFinder(TraceCallStore store,
                                         CallStackTree? tree,
                                         IReadOnlyDictionary<ulong, IteratorTarget> iterators,
                                         HoBtAccessMap hobtAccess,
                                         MemoryMap memoryMap,
                                         Func<ulong, string?> getHoBtName,
                                         ITraceSignatureSource signatures)
{
    private TraceCallStore Store { get; } = store;

    private TraceValueSearch Search { get; } = new(store);

    private Lazy<Dictionary<ulong, CallstackFrame>> Frames { get; } = new(() => GetFrames(tree));

    public static string GetIteratorName(IteratorTarget iterator)
        => $"{iterator.Label} {(iterator.IsPlanObject ? "Plan Object" : "Iterator")}";

    public static string? GetAllocationName(MemoryMapEntry entry)
        => (entry.Site ?? entry.Allocator) is { } allocation ? $"{allocation} Allocation" : null;

    public CallstackFrame? GetFrame(TraceValueUse use)
        => use.First.Node?.Frame is { Resolved: not null } frame ? frame : Frames.Value.GetValueOrDefault(use.FunctionAddress);

    public async Task<TraceValueReferences> FindAsync(ulong value, CancellationToken cancellationToken)
    {
        var uses = await GetDeclaredUsesAsync(await Search.GetValueUsesAsync(value, cancellationToken));

        var functionTypes = await GetFunctionTypesAsync(uses.Where(u => u.Location == ArgumentLayout.ThisRegisterName));

        cancellationToken.ThrowIfCancellationRequested();

        var groups = TraceValueUseGroup.Build(uses, u => functionTypes.GetValueOrDefault(u.FunctionAddress));

        var iterator = iterators.GetValueOrDefault(value);

        return new TraceValueReferences(GetTypeName(value, iterator, groups), iterator, groups);
    }

    public Task<IReadOnlyList<TraceValueCall>> GetCallsAsync(TraceValueUse use, ulong value, CancellationToken cancellationToken)
        => Search.GetTraceValueCallsAsync(use, value, cancellationToken);

    private static Dictionary<ulong, CallstackFrame> GetFrames(CallStackTree? tree)
    {
        var frames = new Dictionary<ulong, CallstackFrame>();

        foreach (var frame in tree?.Nodes().Select(n => n.Frame).OfType<CallstackFrame>() ?? [])
        {
            if (frame.Address != 0 && (!frames.TryGetValue(frame.Address, out var existing) || existing.Resolved is null))
            {
                frames[frame.Address] = frame;
            }
        }

        return frames;
    }

    private async Task<List<TraceValueUse>> GetDeclaredUsesAsync(IReadOnlyList<TraceValueUse> uses)
    {
        var declared = new List<TraceValueUse>(uses.Count);

        foreach (var use in uses)
        {
            if (GetFrame(use) is not { } frame
                || await signatures.GetLayoutAsync(frame) is not { } layout
                || layout.IsDeclared(use.Location))
            {
                declared.Add(use);
            }
        }

        return declared;
    }

    private async Task<Dictionary<ulong, FunctionType?>> GetFunctionTypesAsync(IEnumerable<TraceValueUse> uses)
    {
        var functionTypes = new Dictionary<ulong, FunctionType?>();

        foreach (var use in uses)
        {
            if (functionTypes.ContainsKey(use.FunctionAddress) || GetFrame(use) is not { } frame)
            {
                continue;
            }

            functionTypes[use.FunctionAddress] = await signatures.GetFunctionTypeAsync(frame);
        }

        return functionTypes;
    }

    private string? GetTypeName(ulong value, IteratorTarget? iterator, List<TraceValueUseGroup> groups)
    {
        if (iterator is not null)
        {
            return GetIteratorName(iterator);
        }

        if (hobtAccess.GetHoBtId(value) is { } hobtId && getHoBtName(hobtId) is { } name)
        {
            return $"HoBtAccess ({name})";
        }

        var firstUse = groups.SelectMany(g => g.Uses).MinBy(u => u.First.Position);

        if (firstUse is not null
            && Store.GetCallList(firstUse.FunctionAddress, firstUse.InstanceAddress) is { } calls
            && firstUse.First.CallIndex < calls.Count)
        {
            var call = calls[firstUse.First.CallIndex];

            var moment = firstUse.Location == ArgumentLayout.ReturnRegisterName ? call.ToPosition : call.FromPosition;

            if (memoryMap.GetEntry(value, moment) is { } entry && (entry.ClassName ?? GetAllocationName(entry)) is { } typeName)
            {
                return typeName;
            }

            if (memoryMap.GetContaining(value, moment) is ({ } inside, var offset)
                && (inside.ClassName ?? GetAllocationName(inside)) is { } container)
            {
                return $"{container} + 0x{offset:X}";
            }
        }

        return groups.FirstOrDefault(g => g.Role == TraceValueRole.CalledOn) is { Uses: [var first, ..] }
               && GetFrame(first)?.Resolved?.ClassName is { Length: > 0 } className
            ? className
            : null;
    }
}
