using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Arguments;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Stack;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Values;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.CallStack;

public sealed class CallStoreLookup(TraceCallStore store,
                                  CallStackTree? tree,
                                  IReadOnlyDictionary<ulong, IteratorTarget> iterators,
                                  HoBtAccessMap hobtAccess,
                                  AddressMap addressMap,
                                  MemoryHistoryStore memoryHistory,
                                  MemoryObjectMap memoryObjects,
                                  StorageIdMap storageIds,
                                  SymbolsViewModel symbols) : ITraceSignatureSource
{
    private const string ThisType = "this";

    public TraceCallStore Store { get; } = store;

    private Dictionary<ulong, Task<ResolvedSignature?>> Signatures { get; } = [];

    private TraceReferenceFinder References
        => field ??= new(Store, tree, iterators, hobtAccess, addressMap, memoryObjects, storageIds, this);

    public IteratorTarget? GetIterator(ulong value) => iterators.GetValueOrDefault(value);

    public CallstackFrame? GetFrame(TraceValueUse use) => References.GetFrame(use);

    public StorageId? GetStorageId(ArgumentSlot slot, ulong value, TraceArgumentCall call)
    {
        if (slot.IsHoBtId)
        {
            return storageIds.GetHoBt(value);
        }

        if (ArgumentValue.GetHoBtId(slot.Type, value) is { } hobtId)
        {
            return storageIds.GetHoBt(hobtId);
        }

        if (slot.Type == ThisType || ArgumentValue.IsPointer(slot.Type))
        {
            return hobtAccess.GetHoBtId(value, call.FromPosition) is { } accessed ? storageIds.GetHoBt(accessed) : null;
        }

        return ArgumentValue.IsAddressSized(slot.Type) ? storageIds.GetStorageId(value) : null;
    }

    public ValueDescription? Describe(ArgumentSlot slot, ulong value, TraceArgumentCall call)
    {
        var isPointer = slot.Type == ThisType || ArgumentValue.IsPointer(slot.Type);

        var isAddress = value != 0 && (isPointer || ArgumentValue.IsAddressSized(slot.Type));

        var entry = isAddress ? addressMap.GetEntry(value, GetPosition(slot, call)) : null;

        var isObject = isPointer || entry is not null;

        var containing = isPointer && value != 0 && entry is null ? addressMap.GetContaining(value, GetPosition(slot, call)) : null;

        if (GetStorageId(slot, value, call) is { } storageId)
        {
            return new ValueDescription(storageId.Name, isPointer ? null : storageId.TypeName) { Origin = entry };
        }

        if (!isObject)
        {
            return null;
        }

        var className = entry?.ClassName;

        if (GetIterator(value) is { } iterator)
        {
            return new ValueDescription(TraceReferenceFinder.GetIteratorName(iterator), className) { Origin = entry };
        }

        var address = $"0x{value:X}";

        if (className is not null)
        {
            return new ValueDescription(address, className) { Origin = entry };
        }

        if (containing is ({ } inside, var offset) && (inside.ClassName ?? TraceReferenceFinder.GetAllocationName(inside)) is { } container)
        {
            return new ValueDescription($"0x{inside.Address:X} + 0x{offset:X}", container) { Origin = inside };
        }

        if (slot.Type == ThisType || value == 0)
        {
            return entry is null ? null : new ValueDescription(address) { Origin = entry };
        }

        if (ArgumentValue.GetPointeeClass(slot.Type) is { } declared)
        {
            return new ValueDescription(address, declared) { Origin = entry };
        }

        if ((entry?.Site ?? entry?.Allocator) is { } allocation)
        {
            return new ValueDescription(address, allocation) { Origin = entry };
        }

        return DescribeStackObject(value, GetPosition(slot, call)) ?? DescribeMemoryObject(value, GetPosition(slot, call));
    }

    public async Task<CallSignature?> GetSignatureAsync(CallStackNode node, int callIndex)
    {
        if (node.Frame is not { } frame || CallsOf(node) is not { } calls || callIndex < 0 || callIndex >= calls.Count)
        {
            return null;
        }

        var layout = await GetLayoutAsync(frame);

        var function = FunctionName(frame, frame.Address);

        return await Task.Run(() =>
        {
            var logged = calls[callIndex];

            return new CallSignature(ArgumentRowBuilder.GetSignature(function,
                                                                  layout,
                                                                  logged,
                                                                  Describe,
                                                                  includeReturn: false,
                                                                  includeTypes: true,
                                                                  showThisAddress: false),
                                     ArgumentRowBuilder.ThisOf(layout, logged, Describe),
                                     ArgumentRowBuilder.ReturnOf(layout, logged, Describe, includeType: true));
        });
    }

    public (TracePosition FromPosition, TracePosition ToPosition) GetPositions(CallStackNode node, int callIndex)
        => CallsOf(node) is { } calls && callIndex >= 0 && callIndex < calls.Count
            ? (calls.GetFromPosition(callIndex), calls.GetToPosition(callIndex))
            : (TracePosition.Unknown, TracePosition.Unknown);

    public int GetEnclosingCall(CallStackNode node, TracePosition position, uint thread)
        => CallsOf(node)?.IndexOfEnclosing(position, thread) ?? -1;

    public CallReference? GetOrigin(AddressMapEntry origin)
        => Store.GetNode(origin.CallNodeIndex) is { Frame: not null } node && CallsOf(node) is { } calls
            ? new CallReference(node, origin.CallIndex < calls.Count ? origin.CallIndex : -1)
            : null;

    public Task<ResolvedSignature?> ResolveAsync(CallstackFrame frame)
    {
        if (!Signatures.TryGetValue(frame.Address, out var resolved))
        {
            resolved = ResolveSignatureAsync(frame);

            Signatures[frame.Address] = resolved;
        }

        return resolved;
    }

    public async Task<ArgumentLayout?> GetLayoutAsync(CallstackFrame frame) => (await ResolveAsync(frame))?.Layout;

    public async Task<FunctionType?> GetFunctionTypeAsync(CallstackFrame frame)
        => await symbols.ResolveFrameDecoratedNameAsync(frame) is { } decoratedName ? FunctionSignature.GetFunctionType(decoratedName) : null;

    public Task<TraceValueReferenceResult> FindReferencesAsync(ulong value, CancellationToken cancellationToken)
        => References.FindAsync(value, cancellationToken);

    public async Task<ValueReferences> GetReferencesAsync(ulong value, CancellationToken cancellationToken)
    {
        var found = await FindReferencesAsync(value, cancellationToken);

        return new ValueReferences(found.Type, found.Iterator, [.. found.Groups.Select(g => ValueUseGroup.Create(g, value, GetFrame))]);
    }

    public Task<IReadOnlyList<TraceValueCall>> FindCallsAsync(ValueUseRow row, CancellationToken cancellationToken)
        => References.GetCallsAsync(row.Use, row.Value, cancellationToken);

    public async Task<IReadOnlyList<ValueCallRow>> CallRowsAsync(TraceValueUse use,
                                                                 IReadOnlyList<TraceValueCall> valueCalls,
                                                                 int from,
                                                                 int count,
                                                                 CancellationToken cancellationToken)
    {
        if (Store.GetCallList(use.FunctionAddress, use.InstanceAddress) is not { } calls)
        {
            return [];
        }

        var frame = GetFrame(use);

        var layout = frame is null ? null : await GetLayoutAsync(frame);

        var function = FunctionName(frame, use.FunctionAddress);

        var end = Math.Min(from + count, valueCalls.Count);

        return await Task.Run<IReadOnlyList<ValueCallRow>>(
            () => [.. Enumerable.Range(from, Math.Max(0, end - from)).Select(i => GetCallRow(valueCalls[i], use, function, layout, calls))],
            cancellationToken);
    }

    private TraceCallList? CallsOf(CallStackNode node)
        => node.Frame is { } frame ? Store.GetCallList(frame.Address, frame.InstanceAddress) : null;

    private static string FunctionName(CallstackFrame? frame, ulong address)
    {
        if (frame?.Resolved is { MethodName: { Length: > 0 } method } resolved)
        {
            return resolved.ClassName is { Length: > 0 } className ? $"{className}::{method}" : method;
        }

        return frame is null ? $"0x{address:X}" : QualifiedSymbol.Of(frame);
    }

    private static TracePosition GetPosition(ArgumentSlot slot, TraceArgumentCall call)
        => slot.Index == ArgumentLayout.ReturnRegister ? call.ToPosition : call.FromPosition;

    private ValueDescription? DescribeStackObject(ulong value, TracePosition position)
    {
        if (memoryHistory.GetBlock(value, position) is not (>= 0 and var index)
            || memoryHistory.Blocks[index] is not { IsStack: true } block
            || Store.GetNode(block.CallNodeIndex)?.Frame is not { } frame)
        {
            return null;
        }

        var text = value == block.Address ? $"0x{value:X}" : $"0x{block.Address:X} + 0x{value - block.Address:X}";

        return new ValueDescription(text, FunctionName(frame, frame.Address)) { IsLocal = true };
    }

    private ValueDescription? DescribeMemoryObject(ulong value, TracePosition position)
        => memoryObjects.GetOwner(value, position) is { } owner
           && (owner.Name ?? addressMap.GetEntry(owner.Address, position)?.ClassName) is { } memoryObject
            ? new ValueDescription(owner.GetText(value), memoryObject)
            : null;

    private ValueCallRow GetCallRow(TraceValueCall valueCall,
                                    TraceValueUse use,
                                    string function,
                                    ArgumentLayout? layout,
                                    TraceCallList calls)
        => new(valueCall, use, ArgumentRowBuilder.GetSignature(function, layout, calls[valueCall.CallIndex], Describe, includeTypes: true));

    private async Task<ResolvedSignature?> ResolveSignatureAsync(CallstackFrame frame)
    {
        var signature = await symbols.ResolveFrameSignatureAsync(frame);

        var decoratedName = await symbols.ResolveFrameDecoratedNameAsync(frame);

        if (FunctionSignature.Parse(signature, decoratedName) is not { } parsed)
        {
            return null;
        }

        var layout = KnownParameterLookup.Apply(ArgumentLayout.GetLayout(parsed), frame.Resolved?.ClassName, frame.Resolved?.MethodName);

        return new ResolvedSignature(parsed, signature, layout);
    }
}
