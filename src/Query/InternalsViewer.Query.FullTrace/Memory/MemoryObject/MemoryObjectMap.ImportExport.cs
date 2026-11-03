using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace.Memory.MemoryObject;

public sealed partial class MemoryObjectMap
{
    private const int FormatVersion = 1;

    internal static bool IsSupported(BinaryReader reader) => reader.ReadInt32() == FormatVersion;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(FormatVersion);

        writer.Write(Objects.Count);

        foreach (var (address, info) in Objects)
        {
            writer.Write(address);

            writer.Write(info.Type);

            writer.Write(info.PageSize);
        }
    }

    internal static Dictionary<ulong, MemoryObjectInfo> ReadObjects(BinaryReader reader)
    {
        reader.ReadInt32();

        var count = reader.ReadInt32();

        var objects = new Dictionary<ulong, MemoryObjectInfo>(count);

        for (var index = 0; index < count; index++)
        {
            objects[reader.ReadUInt64()] = new MemoryObjectInfo(reader.ReadString(), reader.ReadInt32());
        }

        return objects;
    }
}
