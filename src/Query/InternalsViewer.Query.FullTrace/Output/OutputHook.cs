using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.Output.SourceParsers;
using InternalsViewer.Query.FullTrace.Output.Sources;
using InternalsViewer.Query.FullTrace.RowBuffers.Accessors;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
using InternalsViewer.Query.FullTrace.RowSources;

namespace InternalsViewer.Query.FullTrace.Output;

internal sealed class OutputHook(OutputFunctions functions, RowSourceHook rowSources, AccessorHook accessors) : ITraceHook
{
    private const int EvaluationContextArgument = 0;
    private const int EvaluationExecutorArgument = 1;

    private const int ReadExecutorArgument = 0;
    private const int SendRowArgument = 0;
    private const int SendValuesArgument = 2;

    private const ulong ExecutorInstructionOffset = 0x20;
    private const ulong ExecutorValuesOffset = 0x28;
    private const ulong ExecutorVariantsOffset = 0x30;
    private const ulong InstructionValueOffset = 0x0C;

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

    private Dictionary<ulong, TraceOutputSource> Sources { get; } = [];

    private Dictionary<ulong, ulong> OutputExecutors { get; } = [];

    private HashSet<ulong> OtherExecutors { get; } = [];

    private Dictionary<ulong, List<TraceOutputSource>> Undecided { get; } = [];

    private Dictionary<ulong, List<TraceOutputSource>> Columns { get; } = [];

    private Dictionary<ulong, ulong> DirectSends { get; } = [];

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
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (Reading.Remove(call.Id, out var read))
        {
            AddColumn(call.Thread, read, replay);
        }
        else if (Frames.TryGetValue(call.Thread, out var frame) && frame.Id == call.Id)
        {
            Frames.Remove(call.Thread);

            EndEvaluation(frame, replay);
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        Reading.Remove(call.Id);

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
                                                                DirectSends.GetValueOrDefault(r)))];
    }

    private static ulong? GetSourceAddress(ITraceReplay replay, ulong executor, bool isVariant)
    {
        var table = executor + (isVariant ? ExecutorVariantsOffset : ExecutorValuesOffset);

        if (!replay.TryRead(executor + ExecutorInstructionOffset, out ulong instruction)
            || !replay.TryRead(instruction + InstructionValueOffset, out uint index)
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
        if (!replay.TryRead(executor + ExecutorValuesOffset, out ulong values)
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
                     : read.IsVariant ? new BufferOutputSource(0, address)
                     : OutputSourceParser.Parse(replay, Classes, address, frame.Context);

            if (address != 0 && source is not UnknownOutputSource)
            {
                Sources[address] = source;
            }
        }

        columns.Add(source);
    }

    private TraceOutputSource GetOwned(TraceOutputSource source, Dictionary<TraceOutputSource, TraceOutputSource> owners)
    {
        if (!owners.TryGetValue(source, out var owned))
        {
            owned = source.Owner != 0 ? source : source with { Owner = GetOwner(source) };

            if (owned is RowsetOutputSource { Ordinal: < 0, Accessor: not 0 } rowset)
            {
                owned = rowset with { Ordinal = accessors.GetOrdinal(rowset.Accessor) };
            }

            owners[source] = owned;
        }

        return owned;
    }

    private ulong GetOwner(TraceOutputSource source)
    {
        if (source is { Context: not 0, Slot: >= 0 } && rowSources.GetOwner(source.Context, source.Slot) is not 0 and var owner)
        {
            return owner;
        }

        return source.Value != 0 ? rowSources.GetCreator(source.Value) : 0;
    }

    private readonly record struct PendingRead(ulong Address, bool IsVariant);

    private sealed record EvaluationFrame(ulong Id, ulong Executor, ulong Context, List<TraceOutputSource>? Columns);
}
