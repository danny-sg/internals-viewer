using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal readonly partial record struct QueryTeardown
{
    private const int FormatVersion = 1;

    internal static void Write(BinaryWriter writer, IReadOnlyList<QueryTeardown> teardowns)
    {
        writer.Write(-FormatVersion);

        writer.Write(teardowns.Count);

        foreach (var teardown in teardowns)
        {
            writer.Write(teardown.Thread);

            TracePointExport.Write(writer, teardown.End);
        }
    }

    internal static QueryTeardown[] Read(BinaryReader reader)
    {
        if (reader.ReadInt32() != -FormatVersion)
        {
            throw new InvalidDataException("The query teardowns were exported in an older format and must be re-recorded");
        }

        var teardowns = new QueryTeardown[reader.ReadInt32()];

        for (var index = 0; index < teardowns.Length; index++)
        {
            teardowns[index] = new QueryTeardown(reader.ReadUInt32(), TracePointExport.Read(reader));
        }

        return teardowns;
    }
}
