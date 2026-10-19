namespace InternalsViewer.Query.FullTrace.Export;

internal static class TracePointExport
{
    public static void Write(BinaryWriter writer, TracePoint point)
    {
        writer.Write(point.Position.Sequence);
        writer.Write(point.Position.Steps);
        writer.Write(point.Instructions);
    }

    public static TracePoint Read(BinaryReader reader)
        => new(new TracePosition(reader.ReadUInt64(), reader.ReadUInt64()), reader.ReadUInt64());
}
