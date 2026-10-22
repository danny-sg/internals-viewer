using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal sealed partial class BitPackedArray
{
    public static BitPackedArray Read(BinaryReader reader)
    {
        var count = reader.ReadInt32();

        var width = reader.ReadInt32();

        return new BitPackedArray(ExportBinary.ReadArray<ulong>(reader), count, width);
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Count);

        writer.Write(Width);

        ExportBinary.WriteArray(writer, Words);
    }
}
