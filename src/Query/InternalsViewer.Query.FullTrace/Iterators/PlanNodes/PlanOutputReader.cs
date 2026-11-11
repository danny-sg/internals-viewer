using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal static class PlanOutputReader
{
    private const int MaximumOutputs = 4096;

    public static IReadOnlyList<IteratorPlanNode> Read(IReadOnlyList<IteratorPlanNode> planNodes,
                                                       MemoryHistoryStore store,
                                                       PlanNodeLayout layout)
    {
        if (store.Blocks.Count == 0 || planNodes.Count == 0)
        {
            return planNodes;
        }

        var planObjects = planNodes.Select(p => p.PlanObject).ToHashSet();

        var outputs = new Dictionary<ulong, IReadOnlyList<PlanOutputColumn>>();

        foreach (var planNode in planNodes.Where(p => p.Constructed is not null).OrderBy(p => p.Constructed!.Value.Position))
        {
            if (!outputs.ContainsKey(planNode.PlanObject))
            {
                var memory = new MemoryHistoryReader(store, planNode.Constructed!.Value.Position);

                outputs[planNode.PlanObject] = Read(memory, planNode.PlanObject, layout, planObjects);
            }
        }

        return [.. planNodes.Select(p => outputs.TryGetValue(p.PlanObject, out var found) ? p with { Outputs = found } : p)];
    }

    internal static IReadOnlyList<PlanOutputColumn> Read(IMemoryReader memory,
                                                         ulong planObject,
                                                         PlanNodeLayout layout,
                                                         IReadOnlySet<ulong> planObjects)
    {
        if (!memory.TryRead(planObject + layout.PlanOutputsOffset, out ulong outputs)
            || outputs < TraceAddress.MinimumAddress
            || !memory.TryRead(outputs + layout.OutputCountOffset, out int count)
            || !memory.TryRead(outputs + layout.OutputColumnsOffset, out ulong columns)
            || count is <= 0 or > MaximumOutputs)
        {
            return [];
        }

        var found = new PlanOutputColumn[count];

        for (var index = 0; index < count; index++)
        {
            if (!memory.TryRead(columns + (ulong)(index * sizeof(ulong)), out ulong reference)
                || reference < TraceAddress.MinimumAddress)
            {
                return [];
            }

            found[index] = ReadColumn(memory, reference, layout, planObjects);
        }

        return found;
    }

    private static PlanOutputColumn ReadColumn(IMemoryReader memory,
                                               ulong reference,
                                               PlanNodeLayout layout,
                                               IReadOnlySet<ulong> planObjects)
    {
        var isStorage = !(memory.TryRead(reference + layout.ColumnProducerOffset, out ulong producer) && planObjects.Contains(producer))
                        && memory.TryRead(reference + layout.StorageColumnProducerOffset, out producer)
                        && planObjects.Contains(producer);

        memory.TryRead(reference + (isStorage ? layout.StorageColumnTypeOffset : layout.ColumnTypeOffset), out ulong header);

        memory.TryRead(reference + (isStorage ? layout.StorageColumnTypeDetailsOffset : layout.ColumnTypeDetailsOffset), out ulong details);

        return new PlanOutputColumn(reference, planObjects.Contains(producer) ? producer : 0, SqlTypeInfo.Create(header, details));
    }
}
