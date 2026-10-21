using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryMap
{
    private const int FormatVersion = 1;

    private const int NoClass = -1;

    internal static bool IsCurrent(BinaryReader reader) => reader.ReadInt32() == -FormatVersion;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        var entries = Entries.Values.SelectMany(e => e).ToList();

        var classes = entries.Select(e => e.ClassName).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        var classIndexes = classes.Select((name, index) => (name, index)).ToDictionary(c => c.name, c => c.index, StringComparer.Ordinal);

        writer.Write(classes.Count);

        foreach (var name in classes)
        {
            writer.Write(name);
        }

        writer.Write(entries.Count);

        foreach (var entry in entries)
        {
            writer.Write(entry.Address);

            writer.Write(entry.CallIndex);

            writer.Write(entry.Owner);

            writer.Write(entry.FromPosition.Sequence);

            writer.Write(entry.FromPosition.Steps);

            writer.Write(entry.ToPosition.Sequence);

            writer.Write(entry.ToPosition.Steps);

            writer.Write(entry.CallNodeIndex);

            writer.Write(entry.Thread);

            writer.Write((byte)entry.Operation);

            writer.Write(entry.ClassName is { } className ? classIndexes[className] : NoClass);

            writer.Write(entry.Bytes);
        }
    }

    internal static MemoryMap Read(BinaryReader reader, Func<int, CallStackNode?> nodeOf)
    {
        if (!IsCurrent(reader))
        {
            throw new InvalidDataException("The memory map was exported before declared types and lengths were recorded and must be "
                                           + "re-recorded");
        }

        var classes = new string[reader.ReadInt32()];

        for (var index = 0; index < classes.Length; index++)
        {
            classes[index] = reader.ReadString();
        }

        var entries = new AddressEntry[reader.ReadInt32()];

        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new AddressEntry(Address: reader.ReadUInt64(),
                                              Frame: 0,
                                              CallIndex: reader.ReadInt32(),
                                              Owner: reader.ReadUInt64(),
                                              FromPosition: new TracePosition(reader.ReadUInt64(), reader.ReadUInt64()),
                                              ToPosition: new TracePosition(reader.ReadUInt64(), reader.ReadUInt64()),
                                              CallNodeIndex: reader.ReadInt32(),
                                              Thread: reader.ReadUInt32(),
                                              Operation: (MemoryMapOperation)reader.ReadByte(),
                                              ClassName: ReadClassName(reader, classes),
                                              Bytes: reader.ReadUInt64());
        }

        return Build(entries, nodeOf);
    }

    private static string? ReadClassName(BinaryReader reader, string[] classes)
    {
        var index = reader.ReadInt32();

        return index == NoClass ? null : classes[index];
    }
}
