using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed class CallLogLookup(TraceCallLog log,
                                  CallStackTree? tree,
                                  IReadOnlyDictionary<ulong, IteratorTarget> iterators,
                                  HoBtAccessMap hobtAccess,
                                  MemoryMap memoryMap,
                                  SymbolsViewModel symbols,
                                  Func<ulong, string?> getHoBtName)
{
    private const string ThisType = "this";

    private const string ReturnLocation = "RAX";

    public TraceCallLog Log { get; } = log;

    private Dictionary<ulong, Task<ArgumentLayout?>> Layouts { get; } = [];

    private Lazy<Dictionary<ulong, CallstackFrame>> Frames { get; } = new(() => GetFrames(tree));

    public IteratorTarget? GetIterator(ulong value) => iterators.GetValueOrDefault(value);

    public CallstackFrame? GetFrame(TraceValueUse use)
        => use.First.Node?.Frame is { Resolved: not null } frame ? frame : Frames.Value.GetValueOrDefault(use.Address);

    public ulong? GetHoBtId(ArgumentSlot slot, ulong value, TraceArgumentCall call)
    {
        if (slot.IsHoBtId)
        {
            return value;
        }

        if (ArgumentValue.GetHoBtId(slot.Type, value) is { } hobtId)
        {
            return hobtId;
        }

        return slot.Type == ThisType || ArgumentValue.IsPointer(slot.Type) ? hobtAccess.GetHoBtId(value, call.Sequence) : null;
    }

    public ValueDescription? Describe(ArgumentSlot slot, ulong value, TraceArgumentCall call)
    {
        if (GetHoBtId(slot, value, call) is { } hobtId && getHoBtName(hobtId) is { } name)
        {
            return name;
        }

        if (slot.Type != ThisType && !ArgumentValue.IsPointer(slot.Type))
        {
            return null;
        }

        var entry = value == 0 ? null : memoryMap.GetEntry(value, GetMoment(slot, call));

        var className = entry?.ClassName;

        if (GetIterator(value) is { } iterator)
        {
            return new ValueDescription(IteratorLabel(iterator), className);
        }

        if (className is not null)
        {
            return new ValueDescription($"0x{value:X}", className);
        }

        if (slot.Type == ThisType || value == 0)
        {
            return null;
        }

        if (ArgumentValue.GetPointeeClass(slot.Type) is { } declared)
        {
            return new ValueDescription($"0x{value:X}", declared);
        }

        return entry?.Allocator is { } allocator ? new ValueDescription($"0x{value:X}", allocator) : null;
    }

    public Task<ArgumentLayout?> GetLayoutAsync(CallstackFrame frame)
    {
        if (!Layouts.TryGetValue(frame.Address, out var layout))
        {
            layout = ResolveLayoutAsync(frame);

            Layouts[frame.Address] = layout;
        }

        return layout;
    }

    public async Task<ValueReferences> GetReferencesAsync(ulong value, CallExclusions exclusions, CancellationToken cancellationToken)
    {
        var uses = await Log.FindAsync(value, cancellationToken, exclusions.IsEmpty ? null : exclusions.Includes);

        var kinds = await ResolveKindsAsync(uses.Where(u => u.Location == "RCX"));

        cancellationToken.ThrowIfCancellationRequested();

        var groups = ValueUseGroup.Build(uses, u => kinds.GetValueOrDefault(u.Address), GetFrame, value);

        var iterator = GetIterator(value);

        return new ValueReferences(GetTypeName(value, iterator, groups), iterator, groups);
    }

    public Task<IReadOnlyList<TraceValueMatch>> FindCallsAsync(ValueUseRow row,
                                                               CallExclusions exclusions,
                                                               CancellationToken cancellationToken)
        => Log.FindCallsAsync(row.Use, row.Value, cancellationToken, exclusions.IsEmpty ? null : exclusions.Includes);

    public async Task<IReadOnlyList<ValueCallRow>> CallRowsAsync(TraceValueUse use,
                                                                 IReadOnlyList<TraceValueMatch> matches,
                                                                 int from,
                                                                 int count,
                                                                 CancellationToken cancellationToken)
    {
        if (Log.CallsOf(use.Address, use.Instance) is not { } calls)
        {
            return [];
        }

        var frame = GetFrame(use);

        var layout = frame is null ? null : await GetLayoutAsync(frame);

        var function = FunctionName(frame, use.Address);

        var end = Math.Min(from + count, matches.Count);

        return await Task.Run<IReadOnlyList<ValueCallRow>>(
            () => [.. Enumerable.Range(from, Math.Max(0, end - from)).Select(i => GetCallRow(matches[i], use, function, layout, calls))],
            cancellationToken);
    }

    private static string FunctionName(CallstackFrame? frame, ulong address)
    {
        if (frame?.Resolved is { MethodName: { Length: > 0 } method } resolved)
        {
            return resolved.ClassName is { Length: > 0 } className ? $"{className}::{method}" : method;
        }

        return frame is null ? $"0x{address:X}" : QualifiedSymbol.Of(frame);
    }

    private static string? GetAllocationName(MemoryMapEntry entry) => entry.Allocator is { } allocator ? $"{allocator} Allocation" : null;

    private static ulong GetMoment(ArgumentSlot slot, TraceArgumentCall call)
        => slot.Index == LocatorReadResolver.ReturnRegister ? call.EndId : call.Id;

    private static string IteratorLabel(IteratorTarget iterator)
        => $"{iterator.Label} {(iterator.IsPlanObject ? "Plan Object" : "Iterator")}";

    private ValueCallRow GetCallRow(TraceValueMatch match, TraceValueUse use, string function, ArgumentLayout? layout, TraceCallList calls)
        => new(match, use, ArgumentRowBuilder.Signature(function, layout, calls[match.Call], Describe));

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

    private async Task<ArgumentLayout?> ResolveLayoutAsync(CallstackFrame frame)
    {
        var signature = await symbols.ResolveFrameSignatureAsync(frame);

        var decoratedName = await symbols.ResolveFrameDecoratedNameAsync(frame);

        return FunctionSignature.Parse(signature, decoratedName) is { } parsed
            ? KnownParameters.Apply(ArgumentLayout.LayoutOf(parsed), frame.Resolved?.ClassName, frame.Resolved?.MethodName)
            : null;
    }

    private async Task<Dictionary<ulong, FunctionKind?>> ResolveKindsAsync(IEnumerable<TraceValueUse> uses)
    {
        var kinds = new Dictionary<ulong, FunctionKind?>();

        foreach (var use in uses)
        {
            if (kinds.ContainsKey(use.Address) || GetFrame(use) is not { } frame)
            {
                continue;
            }

            var decoratedName = await symbols.ResolveFrameDecoratedNameAsync(frame);

            kinds[use.Address] = decoratedName is null ? null : FunctionSignature.KindOf(decoratedName);
        }

        return kinds;
    }

    private string? GetTypeName(ulong value, IteratorTarget? iterator, IReadOnlyList<ValueUseGroup> groups)
    {
        if (iterator is not null)
        {
            return IteratorLabel(iterator);
        }

        if (hobtAccess.GetHoBtId(value) is { } hobtId && getHoBtName(hobtId) is { } name)
        {
            return $"HoBtAccess ({name})";
        }

        var firstUse = groups.SelectMany(g => g.Rows).Select(r => r.Use).MinBy(u => u.First.Sequence);

        if (firstUse is not null
            && Log.CallsOf(firstUse.Address, firstUse.Instance) is { } calls
            && firstUse.First.Call < calls.Count)
        {
            var call = calls[firstUse.First.Call];

            var moment = firstUse.Location == ReturnLocation ? call.EndId : call.Id;

            if (memoryMap.GetEntry(value, moment) is { } entry && (entry.ClassName ?? GetAllocationName(entry)) is { } typeName)
            {
                return typeName;
            }
        }

        return groups.FirstOrDefault(g => g.Role == TraceValueRole.CalledOn) is { Rows: [var first, ..] }
               && GetFrame(first.Use)?.Resolved?.ClassName is { Length: > 0 } className
            ? className
            : null;
    }
}
