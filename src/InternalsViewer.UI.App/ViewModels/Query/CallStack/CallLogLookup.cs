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

    private Dictionary<ulong, Task<ResolvedSignature?>> Signatures { get; } = [];

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
        var isObject = slot.Type == ThisType || ArgumentValue.IsPointer(slot.Type);

        var entry = isObject && value != 0 ? memoryMap.GetEntry(value, GetMoment(slot, call)) : null;

        if (GetHoBtId(slot, value, call) is { } hobtId && getHoBtName(hobtId) is { } name)
        {
            return new ValueDescription(name) { Origin = entry };
        }

        if (!isObject)
        {
            return null;
        }

        var className = entry?.ClassName;

        if (GetIterator(value) is { } iterator)
        {
            return new ValueDescription(IteratorLabel(iterator), className) { Origin = entry };
        }

        var address = $"0x{value:X}";

        if (className is not null)
        {
            return new ValueDescription(address, className) { Origin = entry };
        }

        if (slot.Type == ThisType || value == 0)
        {
            return entry is null ? null : new ValueDescription(address) { Origin = entry };
        }

        if (ArgumentValue.GetPointeeClass(slot.Type) is { } declared)
        {
            return new ValueDescription(address, declared) { Origin = entry };
        }

        return entry?.Allocator is { } allocator ? new ValueDescription(address, allocator) { Origin = entry } : null;
    }

    public async Task<CallSignature?> GetSignatureAsync(CallStackNode node, int call)
    {
        if (node.Frame is not { } frame || CallsOf(node) is not { } calls || call < 0 || call >= calls.Count)
        {
            return null;
        }

        var layout = await GetLayoutAsync(frame);

        var function = FunctionName(frame, frame.Address);

        return await Task.Run(() =>
        {
            var logged = calls[call];

            return new CallSignature(ArgumentRowBuilder.Signature(function,
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

    public ulong GetCallId(CallStackNode node, int call)
        => CallsOf(node) is { } calls && call >= 0 && call < calls.Count ? calls.IdAt(call) : 0;

    public int GetEnclosingCall(CallStackNode node, ulong id) => CallsOf(node)?.IndexOfEnclosing(id) ?? -1;

    public CallReference? GetOrigin(MemoryMapEntry origin)
        => Log.NodeAt(origin.Node) is { Frame: not null } node ? new CallReference(node, CallsOf(node)?.IndexOf(origin.Call) ?? -1) : null;

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

    public async Task<ValueReferences> GetReferencesAsync(ulong value, CancellationToken cancellationToken)
    {
        var uses = await Log.FindAsync(value, cancellationToken);

        var kinds = await ResolveKindsAsync(uses.Where(u => u.Location == "RCX"));

        cancellationToken.ThrowIfCancellationRequested();

        var groups = ValueUseGroup.Build(uses, u => kinds.GetValueOrDefault(u.Address), GetFrame, value);

        var iterator = GetIterator(value);

        return new ValueReferences(GetTypeName(value, iterator, groups), iterator, groups);
    }

    public Task<IReadOnlyList<TraceValueMatch>> FindCallsAsync(ValueUseRow row, CancellationToken cancellationToken)
        => Log.FindCallsAsync(row.Use, row.Value, cancellationToken);

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

    private TraceCallList? CallsOf(CallStackNode node) => node.Frame is { } frame ? Log.CallsOf(frame.Address, frame.Instance) : null;

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

    private async Task<ResolvedSignature?> ResolveSignatureAsync(CallstackFrame frame)
    {
        var signature = await symbols.ResolveFrameSignatureAsync(frame);

        var decoratedName = await symbols.ResolveFrameDecoratedNameAsync(frame);

        if (FunctionSignature.Parse(signature, decoratedName) is not { } parsed)
        {
            return null;
        }

        var layout = KnownParameters.Apply(ArgumentLayout.LayoutOf(parsed), frame.Resolved?.ClassName, frame.Resolved?.MethodName);

        return new ResolvedSignature(parsed, signature, layout);
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
