using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

internal sealed class PlanOutputResultReader(TraceResult trace)
{
    private const string StatementClassPrefix = "CXStmt";

    private const string ExecutorClassName = "CEsExec";

    private const int ExecutorSlot = 1;

    private List<(uint Thread, TracePosition From, TracePosition To)> Evaluations => field ??= GetEvaluations();

    private Dictionary<int, (VariantLocator Variant, TracePosition Position)?> Results { get; } = [];

    public (VariantLocator Variant, TracePosition Position)? Read(TracePosition position)
    {
        var evaluations = Evaluations;

        var index = -1;

        for (int low = 0, high = evaluations.Count - 1; low <= high;)
        {
            var middle = low + (high - low) / 2;

            if (evaluations[middle].To <= position)
            {
                index = middle;

                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (index < 0)
        {
            return null;
        }

        if (!Results.TryGetValue(index, out var result))
        {
            result = FindResult(evaluations[index]);

            Results[index] = result;
        }

        return result;
    }

    private static int FirstAtOrAfter(TraceCallList list, TracePosition position)
    {
        var low = 0;

        var high = list.Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (list.GetFromPosition(middle) < position)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private (VariantLocator Variant, TracePosition Position)? FindResult((uint Thread, TracePosition From, TracePosition To) evaluation)
    {
        (VariantLocator Variant, TracePosition Position)? last = null;

        foreach (var list in trace.CallStore.GetCallLists())
        {
            for (var index = FirstAtOrAfter(list, evaluation.From);
                 index < list.Count && list.GetFromPosition(index) < evaluation.To;
                 index++)
            {
                var end = list.GetToPosition(index);

                if (list.GetThread(index) != evaluation.Thread
                    || !end.IsKnown
                    || end > evaluation.To
                    || last is { } found && end <= found.Position)
                {
                    continue;
                }

                foreach (var locator in list[index].Locators)
                {
                    if (locator.ValueType == LocatorValueType.Variant
                        && new VariantLocator(locator) is { Status: VariantStatus.Value or VariantStatus.Null } variant)
                    {
                        last = (variant, end);
                    }
                }
            }
        }

        return last;
    }

    private List<(uint Thread, TracePosition From, TracePosition To)> GetEvaluations()
    {
        if (GetStatement() is not { } statement)
        {
            return [];
        }

        var calls = new List<(TraceCallList List, int Index)>();

        foreach (var list in trace.CallStore.GetCallLists())
        {
            if (trace.CallStore.GetFirstNode(list)?.Frame?.Function is { ClassName: ExecutorClassName })
            {
                for (var index = 0; index < list.Count; index++)
                {
                    calls.Add((list, index));
                }
            }
        }

        var offset = (ulong)trace.MemoryHistory.Layout.Statement.ExecutorField.Offset;

        var executors = new Dictionary<ulong, bool>();

        var evaluations = new List<(uint Thread, TracePosition From, TracePosition To)>();

        foreach (var (list, index) in calls)
        {
            var executor = list[index].GetSlot(ExecutorSlot);

            var from = list.GetFromPosition(index);

            if (executor == 0 || list.GetToPosition(index) is not { IsKnown: true } to)
            {
                continue;
            }

            if (!executors.TryGetValue(executor, out var isStatement))
            {
                isStatement = new MemoryHistoryReader(trace.MemoryHistory, from).TryRead(statement + offset, out ulong found)
                              && found == executor;

                executors[executor] = isStatement;
            }

            if (isStatement)
            {
                evaluations.Add((list.GetThread(index), from, to));
            }
        }

        evaluations.Sort((x, y) => x.To.CompareTo(y.To));

        return evaluations;
    }

    private ulong? GetStatement()
    {
        var store = trace.MemoryHistory;

        for (var index = 0; index < store.Blocks.Count; index++)
        {
            var block = store.Blocks[index];

            if (trace.AddressMap.GetClassName(block.Address, block.FromPosition, block.ToPosition) is { } name
                && name.StartsWith(StatementClassPrefix, StringComparison.Ordinal))
            {
                return block.Address;
            }
        }

        return null;
    }
}
