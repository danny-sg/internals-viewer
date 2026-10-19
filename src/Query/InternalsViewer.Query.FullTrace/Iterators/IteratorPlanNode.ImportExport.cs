using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal readonly partial record struct IteratorPlanNode
{
    private const int FormatVersion = 2;

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
        }
    }

    internal static IteratorPlanNode[] Read(BinaryReader reader)
    {
        if (reader.ReadInt32() != -FormatVersion)
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
                                                    reader.ReadBoolean() ? TracePointExport.Read(reader) : null);
        }

        return planNodes;
    }
}
