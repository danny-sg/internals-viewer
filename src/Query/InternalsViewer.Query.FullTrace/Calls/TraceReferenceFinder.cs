using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceReferenceFinder(TraceCallStore store,
                                         CallTree? tree,
                                         IReadOnlyDictionary<ulong, IteratorTarget> iterators,
                                         HoBtAccessMap hobtAccess,
                                         AddressMap addressMap,
                                         MemoryObjectMap memoryObjects,
                                         StorageIdMap storageIds,
                                         ITraceSignatureSource signatures)
{
    private TraceCallStore Store { get; } = store;

    private TraceValueSearch Search { get; } = new(store);

    private Lazy<Dictionary<ulong, CallFrame>> Frames { get; } = new(() => GetFrames(tree));

    public static string GetIteratorName(IteratorTarget iterator)
        => $"{iterator.Label} {(iterator.IsPlanObject ? "Plan Object" : "Iterator")}";

    public static string? GetAllocationName(AddressMapEntry entry)
        => (entry.Site ?? entry.Allocator) is { } allocation ? $"{allocation} Allocation" : null;

    public CallFrame? GetFrame(TraceValueUse use)
        => use.First.Node?.Frame is { Function: not null } frame ? frame : Frames.Value.GetValueOrDefault(use.FunctionAddress);

    public async Task<TraceValueReferenceResult> FindAsync(ulong value, CancellationToken cancellationToken)
    {
        var uses = await GetDeclaredUsesAsync(await Search.GetValueUsesAsync(value, cancellationToken));

        var functionTypes = await GetFunctionTypesAsync(uses.Where(u => u.Location == ArgumentLayout.ThisRegisterName));

        cancellationToken.ThrowIfCancellationRequested();

        var groups = TraceValueUseGroup.Build(uses, u => functionTypes.GetValueOrDefault(u.FunctionAddress));

        var iterator = iterators.GetValueOrDefault(value);

        return new TraceValueReferenceResult(GetTypeName(value, iterator, groups), iterator, groups);
    }

    public Task<IReadOnlyList<TraceValueCall>> GetCallsAsync(TraceValueUse use, ulong value, CancellationToken cancellationToken)
        => Search.GetTraceValueCallsAsync(use, value, cancellationToken);

    private static Dictionary<ulong, CallFrame> GetFrames(CallTree? tree)
    {
        var frames = new Dictionary<ulong, CallFrame>();

        foreach (var frame in tree?.Nodes().Select(n => n.Frame).OfType<CallFrame>() ?? [])
        {
            if (frame.Address != 0 && (!frames.TryGetValue(frame.Address, out var existing) || existing.Function is null))
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

        if (hobtAccess.GetHoBtId(value) is { } hobtId && storageIds.GetHoBt(hobtId) is { } hobt)
        {
            return $"HoBtAccess ({hobt.Name})";
        }

        if (storageIds.GetStorageId(value) is { } storageId)
        {
            return $"{storageId.TypeName} {storageId.Name}";
        }

        var firstUse = groups.SelectMany(g => g.Uses).MinBy(u => u.First.Position);

        if (firstUse is not null
            && Store.GetCallList(firstUse.FunctionAddress, firstUse.InstanceAddress) is { } calls
            && firstUse.First.CallIndex < calls.Count)
        {
            var call = calls[firstUse.First.CallIndex];

            var moment = firstUse.Location == ArgumentLayout.ReturnRegisterName ? call.ToPosition : call.FromPosition;

            if (addressMap.GetEntry(value, moment) is { } entry && (entry.ClassName ?? GetAllocationName(entry)) is { } typeName)
            {
                return typeName;
            }

            if (addressMap.GetContaining(value, moment) is ({ } inside, var offset)
                && (inside.ClassName ?? GetAllocationName(inside)) is { } container)
            {
                return $"{container} + 0x{offset:X}";
            }

            if (memoryObjects.GetOwner(value, moment) is { } owner
                && (owner.Name ?? addressMap.GetEntry(owner.Address, moment)?.ClassName) is { } memoryObject)
            {
                return $"{memoryObject} {owner.GetText(value)}";
            }
        }

        return groups.FirstOrDefault(g => g.Role == TraceValueRole.CalledOn) is { Uses: [var first, ..] }
               && GetFrame(first)?.Function?.ClassName is { Length: > 0 } className
            ? className
            : null;
    }
}
