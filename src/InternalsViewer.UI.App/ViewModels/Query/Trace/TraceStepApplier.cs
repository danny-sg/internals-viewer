using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Memory;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Row;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class TraceStepApplier(TraceLayout layout,
                                     TraceRowBuilder rowBuilder,
                                     IReadOnlyDictionary<int, TraceVisualViewModel> visualsByNode,
                                     IReadOnlyDictionary<int, TraceOperatorViewModel> operatorsByNode)
{
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

        SyncHashTables(step);

        visualsByNode.GetValueOrDefault(step.NodeId)?.Apply(step);
    }

    public void Reset()
    {
        foreach (var node in layout.Nodes.Values)
        {
            node.Stream?.Clear();

            foreach (var held in node.HeldRows.Values)
            {
                held.Reset();
            }

            node.HashTable?.Reset();
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
            AccessStep.JoinEmit emit => rowBuilder.ToJoinedModel(emit),
            AccessStep.TopRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.Output { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.ConcatRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.SortRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.Row { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            _ => null
        };

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
                tab.StateItems.Add(new TraceStateItem("Row Count") { Value = "0" });
                break;

            case HashMatchDefinition:
                tab.StateItems.Add(new TraceStateItem("Memory") { Value = "0 KB" });
                break;

            case ConcatenationDefinition concatenation:
                tab.StateItems.Add(new TraceStateItem("Input") { Value = $"1 of {concatenation.Inputs.Count}" });
                tab.StateItems.Add(new TraceStateItem("Rows") { Value = "0" });
                break;
        }
    }

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

    /// <summary>
    /// Replays the positions of a run that was taken in one go rather than step by step
    /// </summary>
    /// <remarks>
    /// The fold is done over the raw positions and only the last of each is handed to a tab. Walking the history through the step by step
    /// path instead would raise a change for every position an operator passed through, which the bindings would each follow.
    /// </remarks>
    public void SyncPositions(IEnumerable<AccessStep> history)
    {
        ResetPositions();

        var open = new HashSet<int>();

        foreach (var step in history)
        {
            if (!operatorsByNode.ContainsKey(step.NodeId))
            {
                continue;
            }

            var position = PositionByNode.GetValueOrDefault(step.NodeId);

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

            PositionByNode[step.NodeId] = position;
        }

        foreach (var (nodeId, position) in PositionByNode)
        {
            var tab = operatorsByNode[nodeId];

            tab.IsOpen = open.Contains(nodeId);
            tab.CurrentPage = position.Page;
            tab.CurrentSlot = position.Slot;
        }
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
            }

            if (iterator is IMemoryBufferIterator buffer)
            {
                tab.SetState("Memory", FormatMemory(buffer.Memory));
            }
        }

        foreach (var tab in operatorsByNode.Values)
        {
            foreach (var row in tab.InputRows)
            {
                row.RowCount = stepper.CountersFor(row.SourceNodeId).RowsOutput.ToString("N0");
            }
        }
    }

    /// <summary>
    /// What a buffer is holding, to the page the engine would allocate it in rather than to the byte the model reached
    /// </summary>
    private static string FormatMemory(BufferMemory memory)
        => $"{memory.PagedKb:N0} KB";

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

    /// <summary>
    /// Brings every hash match's table up to date with the step just taken
    /// </summary>
    public void SyncHashTables(AccessStep? step)
    {
        foreach (var node in layout.Nodes.Values)
        {
            node.HashTable?.Sync(step);
        }
    }

    /// <summary>
    /// Binds each hash match's table to the iterator filling it, which the factory builds fresh on every open
    /// </summary>
    public void AttachHashTables(IteratorStepper stepper)
    {
        foreach (var iterator in Iterators(stepper.Root).OfType<IHashTableIterator>())
        {
            if (layout.Nodes.GetValueOrDefault(iterator.NodeId)?.HashTable is { } hashTable)
            {
                hashTable.Attach(iterator);
            }
        }
    }

    /// <summary>
    /// The running operators, the whole tree rather than the one at the top of it
    /// </summary>
    private static IEnumerable<IIterator> Iterators(IIterator iterator)
    {
        yield return iterator;

        switch (iterator)
        {
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

    private static IndexRecordModel ToRecordModel(IRecord record)
    {
        return TraceVisualViewModel.ToRecordModel(record);
    }
}
