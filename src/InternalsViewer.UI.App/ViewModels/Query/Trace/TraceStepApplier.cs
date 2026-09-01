using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Memory;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Iterators.BatchMode;
using InternalsViewer.Execution.Iterators.Common;
using InternalsViewer.Execution.Iterators.RowMode.Aggregation;
using InternalsViewer.Execution.Iterators.RowMode.Row;
using InternalsViewer.Execution.Iterators.RowMode.Stepping;
using InternalsViewer.Execution.Iterators.RowMode.Windowing;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class TraceStepApplier(TraceLayout layout,
                                     TraceRowBuilder rowBuilder,
                                     IReadOnlyDictionary<int, TraceVisualViewModel> visualsByNode,
                                     IReadOnlyDictionary<int, TraceOperatorViewModel> operatorsByNode,
                                     IReadOnlyDictionary<int, TraceBatchViewModel> batches)
{
    /// <summary>
    /// Raised with the operator that owns the batch a step just moved
    /// </summary>
    public event Action<int>? BatchTouched;

    private Dictionary<int, AccessStrategy?> StrategyBySource { get; } = [];

    private Dictionary<int, (PageAddress? Page, int? Slot)> PositionByNode { get; } = [];

    public AccessStrategy? StrategyFor(int nodeId) => StrategyBySource.GetValueOrDefault(nodeId);

    public void ApplyStep(IteratorStepper stepper, AccessStep step)
    {
        RouteRow(step);

        ApplyPosition(step);

        UpdateStrategies(stepper);

        UpdateOperatorStates(stepper);

        AttachHashTables(stepper);

        SyncHeldRows(stepper);

        SyncAggregates(stepper);

        SyncSegments(stepper);

        SyncHashTables(step);

        SyncBatch(stepper, step);

        visualsByNode.GetValueOrDefault(step.NodeId)?.Apply(step);
    }

    public void Reset()
    {
        foreach (var open in batches.Values)
        {
            open.Clear();
        }

        foreach (var node in layout.Nodes.Values)
        {
            node.Stream?.Clear();

            foreach (var held in node.HeldRows.Values)
            {
                held.Reset();
            }

            node.HashTable?.Reset();

            node.LocalHashTable?.Reset();

            node.Aggregates?.Reset();

            node.Segment?.Reset();
        }

        foreach (var op in operatorsByNode.Values)
        {
            op.StateItems.Clear();

            BuildStateItems(op);

            foreach (var row in op.InputRows)
            {
                row.RowCount = "0";
            }
        }

        foreach (var visual in visualsByNode.Values)
        {
            visual.ResetColumnstore();
        }

        StrategyBySource.Clear();

        ResetPositions();
    }

    public TraceStreamUpdate ComputeStreamUpdate(IEnumerable<AccessStep> history)
    {
        var lastRows = new Dictionary<int, IndexRecordModel>();

        var accumulated = new Dictionary<int, List<IndexRecordModel>>();

        foreach (var step in history)
        {
            if (layout.Nodes.GetValueOrDefault(step.NodeId)?.Stream is not { } stream || ToStreamModel(step) is not { } model)
            {
                continue;
            }

            if (stream.IsAccumulating)
            {
                if (!accumulated.TryGetValue(step.NodeId, out var rows))
                {
                    rows = [];

                    accumulated[step.NodeId] = rows;
                }

                rows.Add(model);
            }
            else
            {
                lastRows[step.NodeId] = model;
            }
        }

        return new TraceStreamUpdate(lastRows, accumulated);
    }

    public void ApplyStreamUpdate(TraceStreamUpdate update)
    {
        foreach (var (nodeId, node) in layout.Nodes)
        {
            if (node.Stream is not { } stream)
            {
                continue;
            }

            if (stream.IsAccumulating)
            {
                stream.Load(update.Accumulated.GetValueOrDefault(nodeId) ?? []);
            }
            else if (update.LastRows.TryGetValue(nodeId, out var last))
            {
                stream.Show(last);
            }
            else
            {
                stream.Clear();
            }
        }
    }

    public void BuildStateItems(TraceOperatorViewModel tab)
    {
        if (layout.Nodes.GetValueOrDefault(tab.NodeId)?.Definition is not { } definition)
        {
            return;
        }

        switch (definition)
        {
            case SortDefinition sort:
                if (sort.TopCount is { } sortTarget)
                {
                    tab.StateItems.Add(new TraceStateItem("Target") { Value = sortTarget.ToString("N0") });
                }

                tab.StateItems.Add(new TraceStateItem("Distinct") { Flag = sort.IsDistinct });
                tab.StateItems.Add(new TraceStateItem("Collected") { Value = "0" });
                tab.StateItems.Add(new TraceStateItem("Output") { Value = "0" });
                tab.StateItems.Add(new TraceStateItem("Memory") { Value = "0 KB" });
                break;

            case TopDefinition top:
                tab.StateItems.Add(new TraceStateItem("Target") { Value = top.RowCount.ToString("N0") });
                break;

            case HashMatchDefinition:
                tab.StateItems.Add(new TraceStateItem("Memory") { Value = "0 KB" });
                break;

            case ConcatenationDefinition concatenation:
                tab.StateItems.Add(new TraceStateItem("Input") { Value = $"1 of {concatenation.Inputs.Count}" });
                tab.StateItems.Add(new TraceStateItem("Rows") { Value = "0" });
                break;

            case StreamAggregateDefinition aggregate:
                if (!aggregate.IsScalar)
                {
                    tab.StateItems.Add(new TraceStateItem("Groups") { Value = "0" });
                }

                break;

            case HashAggregateDefinition or BatchHashAggregateDefinition:
                tab.StateItems.Add(new TraceStateItem("Groups") { Value = "0" });
                tab.StateItems.Add(new TraceStateItem("Memory") { Value = "0 KB" });
                break;

            case FilterDefinition:
                tab.StateItems.Add(new TraceStateItem("Rows Read") { Value = "0" });
                tab.StateItems.Add(new TraceStateItem("Rows Output") { Value = "0" });
                tab.StateItems.Add(new TraceStateItem("Rows Filtered") { Value = "0" });
                break;

            case ComputeScalarDefinition:
                break;
        }
    }

    /// <summary>
    /// Replays the positions of a run that was taken in one go rather than step by step
    /// </summary>
    /// <remarks>
    /// The fold is done over the raw positions and only the last of each is handed to a tab. Walking the history through the step by step
    /// path instead would raise a change for every position an operator passed through, which the bindings would each follow.
    /// </remarks>
    public TracePositionUpdate ComputePositions(IEnumerable<AccessStep> history)
    {
        var positions = new Dictionary<int, (PageAddress? Page, int? Slot)>();

        var open = new HashSet<int>();

        foreach (var step in history)
        {
            if (!operatorsByNode.ContainsKey(step.NodeId))
            {
                continue;
            }

            var position = positions.GetValueOrDefault(step.NodeId);

            switch (step)
            {
                case AccessStep.Open:
                    open.Add(step.NodeId);
                    break;

                case AccessStep.Close:
                    open.Remove(step.NodeId);
                    position = default;
                    break;

                case AccessStep.ReadPage read:
                    position = (read.PageAddress, null);
                    break;

                case AccessStep.Row row:
                    position = (position.Page, row.Slot);
                    break;

                case AccessStep.RowRun run:
                    position = (position.Page, run.ToSlot);
                    break;
            }

            positions[step.NodeId] = position;
        }

        return new TracePositionUpdate(positions, open);
    }

    public void ApplyPositionUpdate(TracePositionUpdate update)
    {
        ResetPositions();

        foreach (var (nodeId, position) in update.Positions)
        {
            PositionByNode[nodeId] = position;

            var tab = operatorsByNode[nodeId];

            tab.IsOpen = update.Open.Contains(nodeId);
            tab.CurrentPage = position.Page;
            tab.CurrentSlot = position.Slot;
        }
    }

    public void UpdateOperatorStates(IteratorStepper stepper)
    {
        foreach (var iterator in Iterators(stepper.Root))
        {
            if (!operatorsByNode.TryGetValue(iterator.NodeId, out var tab))
            {
                continue;
            }

            switch (iterator)
            {
                case TopIterator top:
                    tab.SetState("Row Count", top.RowCount.ToString("N0"));
                    break;

                case SortIterator sort:
                    tab.SetState("Collected", sort.CollectedCount.ToString("N0"));
                    tab.SetState("Output", sort.RowCount.ToString("N0"));
                    break;

                case ConcatenationIterator concatenation:
                    tab.SetState("Input", $"{concatenation.InputNumber} of {concatenation.InputCount}");
                    tab.SetState("Rows", concatenation.RowCount.ToString("N0"));
                    break;

                case StreamAggregateIterator aggregate:
                    tab.SetState("Groups", aggregate.RowCount.ToString("N0"));
                    break;

                case HashAggregateIterator hashAggregate:
                    tab.SetState("Groups", hashAggregate.GroupCount.ToString("N0"));
                    tab.SetState("Rows", hashAggregate.InputRowCount.ToString("N0"));
                    break;

                case ComputeScalarIterator compute:
                    tab.SetState("Rows", compute.RowCount.ToString("N0"));
                    break;
                case FilterIterator filter:
                    tab.SetState("Rows Read", filter.RowCount.ToString("N0"));
                    tab.SetState("Rows Output", filter.PassedCount.ToString("N0"));
                    tab.SetState("Rows Filtered", (filter.RowCount - filter.PassedCount).ToString("N0"));
                    break;
            }

            if (iterator is IMemoryBufferIterator buffer)
            {
                tab.SetState("Memory", FormatMemory(buffer.Memory));
            }
        }

        foreach (var iterator in BatchIterators(stepper).Values.OfType<BatchHashAggregateIterator>())
        {
            if (!operatorsByNode.TryGetValue(iterator.NodeId, out var tab))
            {
                continue;
            }

            tab.SetState("Groups", iterator.GroupCount.ToString("N0"));

            tab.SetState("Rows", iterator.InputRowCount.ToString("N0"));

            tab.SetState("Deep Data", FormatBytes(iterator.DeepDataBytes));

            tab.SetState("Memory", FormatMemory(iterator.Memory));
        }

        foreach (var tab in operatorsByNode.Values)
        {
            foreach (var row in tab.InputRows)
            {
                row.RowCount = stepper.CountersFor(row.SourceNodeId).RowsOutput.ToString("N0");
            }
        }
    }

    public void SyncHeldRows(IteratorStepper stepper)
    {
        foreach (var iterator in Iterators(stepper.Root).OfType<IRowBufferIterator>())
        {
            if (layout.Nodes.GetValueOrDefault(iterator.NodeId)?.HeldRows is not { } heldRows)
            {
                continue;
            }

            foreach (var buffer in iterator.Buffers)
            {
                if (heldRows.TryGetValue(buffer.InputIndex, out var held))
                {
                    held.Sync(buffer.Rows);
                }
            }
        }
    }

    public Dictionary<(int NodeId, int InputIndex), HeldRowsSnapshot> ComputeHeldRows(IteratorStepper stepper)
    {
        var snapshots = new Dictionary<(int NodeId, int InputIndex), HeldRowsSnapshot>();

        foreach (var iterator in Iterators(stepper.Root).OfType<IRowBufferIterator>())
        {
            if (layout.Nodes.GetValueOrDefault(iterator.NodeId)?.HeldRows is not { } heldRows)
            {
                continue;
            }

            foreach (var buffer in iterator.Buffers)
            {
                if (heldRows.ContainsKey(buffer.InputIndex))
                {
                    snapshots[(iterator.NodeId, buffer.InputIndex)] = TraceHeldRowsViewModel.Capture(buffer.Rows);
                }
            }
        }

        return snapshots;
    }

    public void ApplyHeldRows(IReadOnlyDictionary<(int NodeId, int InputIndex), HeldRowsSnapshot> snapshots)
    {
        foreach (var (key, snapshot) in snapshots)
        {
            if (layout.Nodes.GetValueOrDefault(key.NodeId)?.HeldRows.GetValueOrDefault(key.InputIndex) is { } held)
            {
                held.Apply(snapshot);
            }
        }
    }

    public void SyncAggregates(IteratorStepper stepper)
    {
        foreach (var iterator in Iterators(stepper.Root).OfType<StreamAggregateIterator>())
        {
            layout.Nodes.GetValueOrDefault(iterator.NodeId)?.Aggregates?.Sync(iterator.GroupValues,
                                                                             iterator.Running,
                                                                             iterator.CurrentKey,
                                                                             iterator.GroupRowCount,
                                                                             iterator.RowCount);
        }
    }

    public void SyncSegments(IteratorStepper stepper)
    {
        foreach (var iterator in Iterators(stepper.Root).OfType<SegmentIterator>())
        {
            layout.Nodes.GetValueOrDefault(iterator.NodeId)?.Segment?.Sync(iterator.CurrentKeyValues,
                                                                          iterator.RowKeyValues,
                                                                          iterator.SegmentCount);
        }
    }

    /// <summary>
    /// Brings every hash match's table up to date with the step just taken
    /// </summary>
    public void SyncHashTables(AccessStep? step)
    {
        foreach (var node in layout.Nodes.Values)
        {
            node.HashTable?.Sync(step);

            node.LocalHashTable?.Sync(step);
        }
    }

    /// <summary>
    /// Binds each hash match's table to the iterator filling it, which the factory builds fresh on every open
    /// </summary>
    private void SyncBatch(IteratorStepper stepper, AccessStep step)
    {
        if (BatchIterators(stepper).GetValueOrDefault(step.NodeId) is not { } iterator)
        {
            return;
        }

        if (Owner(iterator) is not { } owner || batches.GetValueOrDefault(owner.NodeId) is not { } target)
        {
            return;
        }

        if (ClearsBatch(step))
        {
            target.Clear();

            return;
        }

        if (step is AccessStep.Stopped)
        {
            target.MarkSpent();

            return;
        }

        if (BatchNumber(step) is not { } number)
        {
            return;
        }

        target.Update(iterator.CurrentBatch, number, iterator.OutputVectors);

        BatchTouched?.Invoke(owner.NodeId);
    }

    public void SyncBatches(IteratorStepper stepper)
    {
        foreach (var iterator in BatchIterators(stepper).Values)
        {
            if (Owner(iterator) is not { } owner
                || !ReferenceEquals(owner, iterator)
                || batches.GetValueOrDefault(owner.NodeId) is not { } target)
            {
                continue;
            }

            if (iterator.CurrentBatch is null)
            {
                target.Clear();

                continue;
            }

            target.Update(iterator.CurrentBatch, iterator.BatchNumber, iterator.OutputVectors);

            target.MarkSpent();
        }
    }

    private static IBatchIterator? Owner(IBatchIterator iterator)
    {
        for (var current = iterator; current is not null; current = current.Input)
        {
            if (!ReferenceEquals(current.CurrentBatch, current.Input?.CurrentBatch))
            {
                return current;
            }
        }

        return null;
    }

    private static Dictionary<int, IBatchIterator> BatchIterators(IteratorStepper stepper)
    {
        var found = new Dictionary<int, IBatchIterator>();

        foreach (var adapter in Iterators(stepper.Root).OfType<BatchToRowIterator>())
        {
            for (var iterator = adapter.Source; iterator is not null; iterator = iterator.Input)
            {
                found[iterator.NodeId] = iterator;
            }
        }

        return found;
    }

    /// <summary>
    /// The batch a step belongs to, where the step is one after which a batch holds rows the trace can show
    /// </summary>
    private static long? BatchNumber(AccessStep step) => step switch
    {
        AccessStep.BatchProduced produced => produced.Number,
        AccessStep.BatchFiltered filtered => filtered.Number,
        AccessStep.FilterVector filter => filter.Number,
        AccessStep.ComputeVector compute => compute.Number,
        _ => null
    };

    /// <summary>
    /// Steps that leave no batch in flight
    /// </summary>
    /// <summary>
    /// The row mode iterators a batch pipeline runs inside itself
    /// </summary>
    private static IEnumerable<IIterator> Nested(IBatchIterator batch)
    {
        for (var iterator = batch; iterator is not null; iterator = iterator.Input)
        {
            var inner = iterator switch
            {
                RowToBatchIterator { Source: { } row } => row,
                _ => null
            };

            if (inner is null)
            {
                continue;
            }

            foreach (var found in Iterators(inner))
            {
                yield return found;
            }

            yield break;
        }
    }

    private static bool ClearsBatch(AccessStep step)
        => step is AccessStep.RowGroupOpened
                   or AccessStep.RowGroupSkipped
                   or AccessStep.SegmentElimination
                   or AccessStep.Close;

    public void AttachHashTables(IteratorStepper stepper)
    {
        foreach (var iterator in Iterators(stepper.Root).OfType<IHashTableIterator>())
        {
            if (layout.Nodes.GetValueOrDefault(iterator.NodeId)?.HashTable is { } hashTable)
            {
                hashTable.Attach(iterator, iterator.NodeId);
            }
        }

        foreach (var iterator in BatchIterators(stepper).Values.OfType<BatchHashAggregateIterator>())
        {
            if (layout.Nodes.GetValueOrDefault(iterator.NodeId)?.HashTable is { } hashTable)
            {
                hashTable.Attach(iterator, iterator.NodeId);
            }

            if (layout.Nodes.GetValueOrDefault(iterator.NodeId)?.LocalHashTable is { } localHashTable)
            {
                localHashTable.Attach(iterator.LocalHashTable, iterator.NodeId);
            }
        }
    }

    /// <summary>
    /// Takes the strategy each input settled on once it was opened, so a tab can show its own rather than the tree's
    /// </summary>
    /// <remarks>
    /// A correlated inner has no strategy until its first rebind plans a descent, so this is called again as the walk proceeds and leaves
    /// what it already found alone.
    /// </remarks>
    public void UpdateStrategies(IteratorStepper stepper)
    {
        foreach (var iterator in Iterators(stepper.Root))
        {
            if (iterator.Strategy is { } strategy && visualsByNode.ContainsKey(iterator.NodeId))
            {
                StrategyBySource[iterator.NodeId] = strategy;
            }
        }
    }

    private void RouteRow(AccessStep step)
    {
        if (layout.Nodes.GetValueOrDefault(step.NodeId)?.Stream is not { } stream || ToStreamModel(step) is not { } model)
        {
            return;
        }

        stream.Show(model);
    }

    private IndexRecordModel? ToStreamModel(AccessStep step)
        => step switch
        {
            AccessStep.JoinEmit emit
                => rowBuilder.ToJoinedModel(emit),
            AccessStep.TopRow { EmittedRecord: { } emitted }
                => ToRecordModel(emitted),
            AccessStep.Output { EmittedRecord: { } emitted }
                => ToRecordModel(emitted),
            AccessStep.ConcatRow { EmittedRecord: { } emitted }
                => ToRecordModel(emitted),
            AccessStep.SortRow { EmittedRecord: { } emitted }
                => ToRecordModel(emitted),
            AccessStep.AggregateEmit { EmittedRecord: { } emitted }
                => ToRecordModel(emitted),
            AccessStep.ComputeRow { EmittedRecord: { } emitted }
                => ToRecordModel(emitted),
            AccessStep.Row { EmittedRecord: { } emitted }
                => ToRecordModel(emitted),
            _ => null
        };

    /// <summary>
    /// Follows where an access path stands, which is the page it is reading and the slot it reached on it
    /// </summary>
    /// <remarks>
    /// The iterators publish the page but not the slot, because the slot is a position within a walk rather than state an iterator keeps.
    /// It is read off the steps instead, which is also what makes a page read reset it.
    /// </remarks>
    private void ApplyPosition(AccessStep step)
    {
        if (!operatorsByNode.TryGetValue(step.NodeId, out var tab))
        {
            return;
        }

        var position = PositionByNode.GetValueOrDefault(step.NodeId);

        switch (step)
        {
            case AccessStep.Open:
                tab.IsOpen = true;
                break;

            case AccessStep.Close:
                tab.IsOpen = false;
                position = default;
                break;

            case AccessStep.ReadPage read:
                position = (read.PageAddress, null);
                break;

            case AccessStep.Row row:
                position = (position.Page, row.Slot);
                break;

            case AccessStep.RowRun run:
                position = (position.Page, run.ToSlot);
                break;
        }

        PositionByNode[step.NodeId] = position;

        tab.CurrentPage = position.Page;
        tab.CurrentSlot = position.Slot;
    }

    private void ResetPositions()
    {
        PositionByNode.Clear();

        foreach (var tab in operatorsByNode.Values)
        {
            tab.IsOpen = false;
            tab.CurrentPage = null;
            tab.CurrentSlot = null;
        }
    }

    /// <summary>
    /// What a buffer is holding, to the page the engine would allocate it in rather than to the byte the model reached
    /// </summary>
    private static string FormatMemory(BufferMemory memory)
        => $"{memory.PagedKb:N0} KB";

    private static string FormatBytes(long bytes)
        => bytes < 1024 ? $"{bytes:N0} B" : $"{bytes / 1024D:N0} KB";

    /// <summary>
    /// The running operators, the whole tree rather than the one at the top of it
    /// </summary>
    private static IEnumerable<IIterator> Iterators(IIterator iterator)
    {
        yield return iterator;

        switch (iterator)
        {
            case BatchToRowIterator { Source: { } source }:
                foreach (var found in Nested(source))
                {
                    yield return found;
                }

                break;

            case IJoinIterator join:
                if (join.Outer?.Iterator is { } outer)
                {
                    foreach (var found in Iterators(outer))
                    {
                        yield return found;
                    }
                }

                if (join.Inner?.Iterator is { } inner)
                {
                    foreach (var found in Iterators(inner))
                    {
                        yield return found;
                    }
                }

                break;

            case IMultiInputIterator multi:
                foreach (var input in multi.Inputs)
                {
                    foreach (var found in Iterators(input))
                    {
                        yield return found;
                    }
                }

                break;

            case IUnaryIterator { Input: { } input }:
                foreach (var found in Iterators(input))
                {
                    yield return found;
                }

                break;
        }
    }

    private static IndexRecordModel ToRecordModel(IRecord record)
    {
        return TraceVisualViewModel.ToRecordModel(record);
    }
}
