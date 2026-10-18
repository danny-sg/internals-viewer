using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.Output;

internal sealed class OutputHook(OutputFunctions functions) : ITraceHook
{
    private const int EvaluationContextArgument = 0;

    private const int EvaluationExecutorArgument = 1;

    private const int ReadExecutorArgument = 0;

    private const int SendRowArgument = 0;

    private const int SendValuesArgument = 2;

    private const int IteratorArgument = 0;

    private const int SlotWriterArgument = 0;

    private const int SlotWriterContextArgument = 1;

    private const ulong LayoutExecutorInstructionOffset = 0x20;

    private const ulong LayoutExecutorValuesOffset = 0x28;

    private const ulong LayoutExecutorVariantsOffset = 0x30;

    private const ulong LayoutInstructionValueOffset = 0x0C;

    private const ulong LayoutIteratorRowsetOffset = 0x48;

    private const ulong LayoutIteratorBufferOffset = 0x80;

    private const ulong LayoutSwitchStateOffset = 0x08;

    private const ulong LayoutSwitchPrimaryOffset = 0x10;

    private const ulong LayoutCachedSwitchInnerOffset = 0x08;

    private const ulong LayoutCachedSwitchStateOffset = 0x10;

    private const ulong LayoutHashOffsetOffset = 0x10;

    private const ulong LayoutHashOrdinalOffset = 0x14;

    private const ulong LayoutHashLengthOffset = 0x2A;

    private const ulong LayoutHashSlotOffset = 0x2C;

    private const ulong LayoutVariantOffset = 0x08;

    private const ulong LayoutTableRowsetOffset = 0x08;

    private const ulong LayoutTableSlotOffset = 0x10;

    private const ulong LayoutRedirectIndexOffset = 0x08;

    private const ulong LayoutRedirectHelperOffset = 0x10;

    private const ulong LayoutHelperRowOffset = 0x78;

    private const ulong LayoutVariantTableSlotOffset = 0x08;

    private const ulong LayoutVariantTableOrdinalOffset = 0x0C;

    private const ulong ValueSize = 0x20;

    private const int MaximumDepth = 4;

    private const ulong MaximumOwnerDistance = 0x400;

    private const ulong MaximumBufferBytes = ValueSize * 256;

    private const TraceHookEvents EntryAndReturn = TraceHookEvents.Entry | TraceHookEvents.Return;

    public IEnumerable<TraceHookFunction> Functions
        => functions.IsComplete
            ? [.. functions.Functions.Select(f => new TraceHookFunction(f.Key, f.Value == OutputFunctionType.Send
                                                                                    ? TraceHookEvents.Entry
                                                                                    : EntryAndReturn))]
            : [];

    private IReadOnlyDictionary<ulong, OutputValueClass> Classes => functions.Classes;

    private Dictionary<uint, EvaluationFrame> Frames { get; } = [];

    private Dictionary<ulong, PendingRead> Reading { get; } = [];

    private Dictionary<ulong, ulong> Creating { get; } = [];

    private Dictionary<ulong, ulong> Fetching { get; } = [];

    private Dictionary<ulong, ulong> TableOwners { get; } = [];

    private Dictionary<ulong, IteratorStorage> Storage { get; } = [];

    private Dictionary<ulong, TraceOutputSource> Sources { get; } = [];

    private Dictionary<ulong, ulong> OutputExecutors { get; } = [];

    private HashSet<ulong> OtherExecutors { get; } = [];

    private Dictionary<ulong, List<TraceOutputSource>> Undecided { get; } = [];

    private Dictionary<ulong, List<TraceOutputSource>> Columns { get; } = [];

    private Dictionary<ulong, ulong> DirectSends { get; } = [];

    private Dictionary<uint, Stack<OpenIterator>> OpenIterators { get; } = [];

    private Dictionary<(ulong Context, int Slot), ulong> SlotOwners { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.Functions.TryGetValue(call.Function, out var type))
        {
            return;
        }

        switch (type)
        {
            case OutputFunctionType.Evaluation:
                StartEvaluation(call);
                break;

            case OutputFunctionType.ValueRead or OutputFunctionType.VariantRead:
                StartRead(call, type == OutputFunctionType.VariantRead, replay);
                break;

            case OutputFunctionType.Send when !Frames.ContainsKey(call.Thread):
                DirectSends.TryAdd(call.Arguments[SendRowArgument], call.Arguments[SendValuesArgument]);
                break;

            case OutputFunctionType.Creator:
                Creating[call.Id] = call.Arguments[IteratorArgument];
                break;

            case OutputFunctionType.Iterator:
                StartIterator(call, replay);
                break;

            case OutputFunctionType.SlotWriter:
                AddSlotOwner(call, replay);
                break;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        EndIterator(call.Thread, call.Id);

        if (Reading.Remove(call.Id, out var read))
        {
            AddColumn(call.Thread, read, replay);
        }
        else if (Creating.Remove(call.Id, out var iterator))
        {
            if (call.ReturnValue >= ColumnValueParser.SmallestAddress)
            {
                TableOwners[call.ReturnValue] = iterator;
            }
        }
        else if (Fetching.Remove(call.Id, out var fetched))
        {
            AddStorage(replay, fetched);
        }
        else if (Frames.TryGetValue(call.Thread, out var frame) && frame.Id == call.Id)
        {
            Frames.Remove(call.Thread);

            EndEvaluation(frame, replay);
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        EndIterator(call.Thread, call.Id);

        Reading.Remove(call.Id);

        Creating.Remove(call.Id);

        Fetching.Remove(call.Id);

        if (Frames.TryGetValue(call.Thread, out var frame) && frame.Id == call.Id)
        {
            Frames.Remove(call.Thread);
        }
    }

    public List<TraceOutputStructure> Build()
    {
        var owners = new Dictionary<TraceOutputSource, TraceOutputSource>(ReferenceEqualityComparer.Instance);

        return [.. Columns.Keys
                          .Union(DirectSends.Keys)
                          .Select(r => new TraceOutputStructure(r,
                                                                Columns.TryGetValue(r, out var columns)
                                                                    ? [.. columns.Select(c => GetOwned(c, owners))]
                                                                    : [],
                                                                DirectSends.TryGetValue(r, out var values) ? GetBufferOwner(values) : 0))];
    }

    private static ulong? GetSourceAddress(ITraceReplay replay, ulong executor, bool isVariant)
    {
        var table = executor + (isVariant ? LayoutExecutorVariantsOffset : LayoutExecutorValuesOffset);

        if (!replay.TryRead(executor + LayoutExecutorInstructionOffset, out ulong instruction)
            || !replay.TryRead(instruction + LayoutInstructionValueOffset, out uint index)
            || !replay.TryRead(table, out ulong entries)
            || !replay.TryRead(entries + index * sizeof(ulong), out ulong address)
            || address < ColumnValueParser.SmallestAddress)
        {
            return null;
        }

        return address;
    }

    private void StartEvaluation(in TraceHookCall call)
    {
        if (Frames.ContainsKey(call.Thread))
        {
            return;
        }

        var executor = call.Arguments[EvaluationExecutorArgument];

        Frames[call.Thread] = new EvaluationFrame(call.Id,
                                                  executor,
                                                  call.Arguments[EvaluationContextArgument],
                                                  IsKnown(executor) ? null : []);
    }

    private void StartRead(in TraceHookCall call, bool isVariant, ITraceReplay replay)
    {
        if (Frames.TryGetValue(call.Thread, out var frame)
            && frame.Columns is not null
            && frame.Executor == call.Arguments[ReadExecutorArgument])
        {
            Reading[call.Id] = new PendingRead(GetSourceAddress(replay, frame.Executor, isVariant) ?? 0, isVariant);
        }
    }

    private bool IsKnown(ulong executor)
        => OtherExecutors.Contains(executor)
           || Undecided.ContainsKey(executor)
           || (OutputExecutors.TryGetValue(executor, out var row) && Columns.ContainsKey(row));

    private ulong? GetOutputRow(ITraceReplay replay, ulong executor)
    {
        if (!replay.TryRead(executor + LayoutExecutorValuesOffset, out ulong values)
            || !replay.TryRead(values, out ulong first)
            || !replay.TryRead(first, out ulong vtable))
        {
            return null;
        }

        return Classes.TryGetValue(vtable, out var valueClass) && valueClass == OutputValueClass.OutputRow ? first : 0;
    }

    private void EndEvaluation(EvaluationFrame frame, ITraceReplay replay)
    {
        if (frame.Columns is null)
        {
            if (Undecided.TryGetValue(frame.Executor, out var undecided))
            {
                Classify(frame.Executor, undecided, replay);
            }

            return;
        }

        if (frame.Columns.Count > 0)
        {
            Classify(frame.Executor, frame.Columns, replay);
        }
    }

    private void Classify(ulong executor, List<TraceOutputSource> columns, ITraceReplay replay)
    {
        switch (GetOutputRow(replay, executor))
        {
            case null:
                Undecided.TryAdd(executor, columns);
                break;

            case 0:
                Undecided.Remove(executor);

                OtherExecutors.Add(executor);
                break;

            case var row:
                Undecided.Remove(executor);

                OutputExecutors[executor] = row.Value;

                Columns.TryAdd(row.Value, columns);
                break;
        }
    }

    private void AddColumn(uint thread, PendingRead read, ITraceReplay replay)
    {
        if (!Frames.TryGetValue(thread, out var frame) || frame.Columns is not { } columns)
        {
            return;
        }

        var address = read.Address != 0 ? read.Address : GetSourceAddress(replay, frame.Executor, read.IsVariant) ?? 0;

        if (address == 0 || !Sources.TryGetValue(address, out var source))
        {
            source = address == 0 ? TraceOutputSource.Unknown
                     : read.IsVariant ? new TraceOutputSource(OutputSourceType.Buffer, 0, address)
                     : GetSource(replay, address, frame.Context, 0, 0);

            if (address != 0 && source.Type != OutputSourceType.Unknown)
            {
                Sources[address] = source;
            }
        }

        columns.Add(source);
    }

    private TraceOutputSource GetSource(ITraceReplay replay, ulong value, ulong context, ulong state, int depth)
    {
        if (depth > MaximumDepth || !replay.TryRead(value, out ulong vtable) || !Classes.TryGetValue(vtable, out var valueClass))
        {
            return TraceOutputSource.Unknown with { State = state };
        }

        switch (valueClass)
        {
            case OutputValueClass.HashSwitch
                when replay.TryRead(value + LayoutSwitchStateOffset, out ulong switchState)
                     && replay.TryRead(value + LayoutSwitchPrimaryOffset, out ulong primary):
                return GetSource(replay, primary, context, state == 0 ? switchState : state, depth + 1);

            case OutputValueClass.HashCachedSwitch
                when replay.TryRead(value + LayoutCachedSwitchInnerOffset, out ulong inner)
                     && replay.TryRead(value + LayoutCachedSwitchStateOffset, out ulong cachedState):
                return GetSource(replay, inner, context, state == 0 ? cachedState : state, depth + 1);

            case OutputValueClass.Hash
                when replay.TryRead(value + LayoutHashSlotOffset, out int slot)
                     && replay.TryRead(value + LayoutHashOffsetOffset, out int offset)
                     && replay.TryRead(value + LayoutHashOrdinalOffset, out ushort ordinal)
                     && replay.TryRead(value + LayoutHashLengthOffset, out ushort length):
                return new TraceOutputSource(OutputSourceType.HashRecord, value, 0)
                {
                    Slot = slot,
                    Offset = offset,
                    Ordinal = ordinal,
                    Length = length,
                    State = state
                };

            case OutputValueClass.Table
                when replay.TryRead(value + LayoutTableRowsetOffset, out ulong rowset)
                     && replay.TryRead(value + LayoutTableSlotOffset, out int rowsetSlot):
                return new TraceOutputSource(OutputSourceType.Rowset, value, rowset) { Slot = rowsetSlot, State = state };

            case OutputValueClass.BatchRedirect
                when replay.TryRead(value + LayoutRedirectIndexOffset, out int index)
                     && replay.TryRead(value + LayoutRedirectHelperOffset, out ulong helper):
                var row = replay.TryRead(helper + LayoutHelperRowOffset, out ulong helperRow) ? helperRow + (ulong)index * ValueSize : 0;

                return new TraceOutputSource(OutputSourceType.Buffer, value, row) { Ordinal = index, Owner = helper, State = state };

            case OutputValueClass.VariantTable
                when replay.TryRead(value + LayoutVariantTableSlotOffset, out int tableSlot)
                     && replay.TryRead(value + LayoutVariantTableOrdinalOffset, out int tableOrdinal):
                var tableRow = replay.TryRead(context + (ulong)tableSlot * sizeof(ulong), out ulong variants)
                    ? variants + (ulong)tableOrdinal * ValueSize
                    : 0;

                return new TraceOutputSource(OutputSourceType.Buffer, value, tableRow)
                {
                    Slot = tableSlot,
                    Ordinal = tableOrdinal,
                    State = context
                };

            case OutputValueClass.Constant when replay.TryRead(value + LayoutVariantOffset, out ulong variant):
                return new TraceOutputSource(OutputSourceType.Buffer, value, variant) { State = state };

            default:
                return TraceOutputSource.Unknown with { State = state };
        }
    }

    private void StartIterator(in TraceHookCall call, ITraceReplay replay)
    {
        var iterator = call.Arguments[IteratorArgument];

        if (!OpenIterators.TryGetValue(call.Thread, out var iterators))
        {
            iterators = [];

            OpenIterators[call.Thread] = iterators;
        }

        iterators.Push(new OpenIterator(call.Id, iterator));

        if (!AddStorage(replay, iterator))
        {
            Fetching[call.Id] = iterator;
        }
    }

    private void EndIterator(uint thread, ulong id)
    {
        if (OpenIterators.TryGetValue(thread, out var iterators) && iterators.TryPeek(out var open) && open.Id == id)
        {
            iterators.Pop();
        }
    }

    private void AddSlotOwner(in TraceHookCall call, ITraceReplay replay)
    {
        var writer = call.Arguments[SlotWriterArgument];

        if (!OpenIterators.TryGetValue(call.Thread, out var iterators)
            || !iterators.TryPeek(out var open)
            || !replay.TryRead(writer, out ulong vtable)
            || Classes.GetValueOrDefault(vtable) is not (OutputValueClass.VariantTable or OutputValueClass.VariantTableRow)
            || !replay.TryRead(writer + LayoutVariantTableSlotOffset, out int slot))
        {
            return;
        }

        SlotOwners[(call.Arguments[SlotWriterContextArgument], slot)] = open.Iterator;
    }

    private bool AddStorage(ITraceReplay replay, ulong iterator)
    {
        if (Storage.TryGetValue(iterator, out var known) && known is { Rowset: not 0, Buffer: not 0 })
        {
            return true;
        }

        var rowset = known.Rowset;

        var buffer = known.Buffer;

        if (rowset == 0)
        {
            replay.TryRead(iterator + LayoutIteratorRowsetOffset, out rowset);
        }

        if (buffer == 0)
        {
            replay.TryRead(iterator + LayoutIteratorBufferOffset, out buffer);
        }

        Storage[iterator] = new IteratorStorage(rowset, buffer);

        return rowset != 0 && buffer != 0;
    }

    private TraceOutputSource GetOwned(TraceOutputSource source, Dictionary<TraceOutputSource, TraceOutputSource> owners)
    {
        if (!owners.TryGetValue(source, out var owned))
        {
            owned = source.Owner != 0 ? source : source with { Owner = GetOwner(source) };

            owners[source] = owned;
        }

        return owned;
    }

    private ulong GetOwner(TraceOutputSource source)
        => source.Type switch
        {
            OutputSourceType.HashRecord => GetNearest(Storage.Keys, source.State, MaximumOwnerDistance),
            OutputSourceType.Rowset when TableOwners.TryGetValue(source.Value, out var creator) => creator,
            OutputSourceType.Rowset => Storage.FirstOrDefault(s => s.Value.Rowset == source.Address).Key,
            OutputSourceType.Buffer when SlotOwners.TryGetValue((source.State, source.Slot), out var writer) => writer,
            OutputSourceType.Buffer => GetBufferOwner(source.Address),
            _ => 0
        };

    private ulong GetBufferOwner(ulong address) => GetNearest(Storage.Keys, address, MaximumBufferBytes, k => Storage[k].Buffer);

    private static ulong GetNearest(IEnumerable<ulong> candidates, ulong address, ulong distance, Func<ulong, ulong>? getStart = null)
    {
        ulong best = 0;

        ulong bestStart = 0;

        foreach (var candidate in candidates)
        {
            var start = getStart?.Invoke(candidate) ?? candidate;

            if (start != 0 && start <= address && address - start < distance && start >= bestStart)
            {
                best = candidate;

                bestStart = start;
            }
        }

        return best;
    }

    private readonly record struct PendingRead(ulong Address, bool IsVariant);

    private readonly record struct IteratorStorage(ulong Rowset, ulong Buffer);

    private readonly record struct OpenIterator(ulong Id, ulong Iterator);

    private sealed record EvaluationFrame(ulong Id, ulong Executor, ulong Context, List<TraceOutputSource>? Columns);
}
