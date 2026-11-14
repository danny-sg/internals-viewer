using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

internal static class PlanOutputReader
{
    private const int MaximumOutputs = 4096;

    private const int MaximumRedirects = 4;

    public static IReadOnlyList<IteratorPlanNode> Read(IReadOnlyList<IteratorPlanNode> planNodes, MemoryHistoryStore store)
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

                outputs[planNode.PlanObject] = Read(memory, planNode.PlanObject, planObjects);
            }
        }

        return [.. planNodes.Select(p => outputs.TryGetValue(p.PlanObject, out var found) ? p with { Outputs = found } : p)];
    }

    internal static IReadOnlyList<PlanOutputColumn> Read(ITraceMemoryReader memory, ulong planObject, IReadOnlySet<ulong> planObjects)
    {
        var layout = memory.Layout;

        if (!memory.TryRead(planObject + (ulong)layout.PlanObject.OutputColumnsField.Offset, out ulong outputs)
            || outputs < TraceAddress.MinimumAddress
            || !memory.TryRead(outputs + (ulong)layout.OutputList.CountField.Offset, out int count)
            || !memory.TryRead(outputs + (ulong)layout.OutputList.ColumnsField.Offset, out ulong columns)
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

            found[index] = ReadColumn(memory, reference, layout.ColumnReference, planObjects);
        }

        return found;
    }

    private static PlanOutputColumn ReadColumn(ITraceMemoryReader memory,
                                               ulong reference,
                                               EngineLayout.ColumnReferenceLayout layout,
                                               IReadOnlySet<ulong> planObjects,
                                               int depth = 0)
    {
        var hasProducer = memory.TryRead(reference + (ulong)layout.ProducerField.Offset, out ulong producer)
                          && planObjects.Contains(producer);

        var isStorage = !hasProducer
                        && memory.TryRead(reference + (ulong)layout.StorageProducerField.Offset, out producer)
                        && planObjects.Contains(producer);

        if (!hasProducer
            && !isStorage
            && depth < MaximumRedirects
            && memory.TryRead(reference + (ulong)layout.RedirectProducerField.Offset, out ulong redirectProducer)
            && planObjects.Contains(redirectProducer)
            && memory.TryRead(reference + (ulong)layout.RedirectTargetField.Offset, out ulong target)
            && target >= TraceAddress.MinimumAddress)
        {
            return ReadColumn(memory, target, layout, planObjects, depth + 1) with { Reference = reference, Producer = redirectProducer };
        }

        var typeField = isStorage ? layout.StorageTypeField : layout.TypeField;

        var detailsField = isStorage ? layout.StorageTypeDetailsField : layout.TypeDetailsField;

        memory.TryRead(reference + (ulong)typeField.Offset, out ulong header);

        memory.TryRead(reference + (ulong)detailsField.Offset, out ulong details);

        return new PlanOutputColumn(reference, planObjects.Contains(producer) ? producer : 0, SqlTypeInfo.Create(header, details));
    }
}
