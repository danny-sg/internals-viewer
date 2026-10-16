using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Export;

internal static class ExportBinary
{
    public static void WriteArray<T>(BinaryWriter writer, T[] values) where T : unmanaged
        => WriteArray(writer, (ReadOnlySpan<T>)values);

    public static void WriteArray<T>(BinaryWriter writer, ReadOnlySpan<T> values) where T : unmanaged
    {
        writer.Write(values.Length);

        writer.Write(MemoryMarshal.AsBytes(values));
    }

    public static T[] ReadArray<T>(BinaryReader reader) where T : unmanaged
    {
        var values = new T[reader.ReadInt32()];

        reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(values.AsSpan()));

        return values;
    }
}
