using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal readonly partial record struct IteratorPlanNode
{
    private const int FormatVersion = 3;

    private const int PreviousVersion = 2;

    internal static void Write(BinaryWriter writer, IReadOnlyList<IteratorPlanNode> planNodes)
    {
        writer.Write(-FormatVersion);

        writer.Write(planNodes.Count);

        foreach (var planNode in planNodes)
        {
            writer.Write(planNode.Iterator);
            writer.Write(planNode.PlanObject);
            writer.Write(planNode.BatchPlanObject);
            writer.Write(planNode.Query);
            writer.Write(planNode.NodeId);
            writer.Write(planNode.Constructed.HasValue);

            if (planNode.Constructed is { } constructed)
            {
                TracePointExport.Write(writer, constructed);
            }

            writer.Write(planNode.IsBatchHelper);
        }
    }

    internal static IteratorPlanNode[] Read(BinaryReader reader)
    {
        var version = -reader.ReadInt32();

        if (version is not (FormatVersion or PreviousVersion))
        {
            throw new InvalidDataException("The plan nodes were exported in an older format and must be re-recorded");
        }

        var planNodes = new IteratorPlanNode[reader.ReadInt32()];

        for (var index = 0; index < planNodes.Length; index++)
        {
            planNodes[index] = new IteratorPlanNode(reader.ReadUInt64(),
                                                    reader.ReadUInt64(),
                                                    reader.ReadUInt64(),
                                                    reader.ReadUInt64(),
                                                    reader.ReadInt32(),
                                                    reader.ReadBoolean() ? TracePointExport.Read(reader) : null,
                                                    version == FormatVersion && reader.ReadBoolean());
        }

        return planNodes;
    }
}
