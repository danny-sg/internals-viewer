using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryMap
{
    private const int FormatVersion = 2;

    private const int FirstVersion = 1;

    private const int SiteVersion = 2;

    private const int NoName = -1;

    internal static bool IsSupported(BinaryReader reader) => ReadVersion(reader) is >= FirstVersion and <= FormatVersion;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        var entries = GetEntries().ToList();

        var names = entries.SelectMany(e => new[] { e.ClassName, e.Site }).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        var nameIndexes = names.Select((name, index) => (name, index)).ToDictionary(n => n.name, n => n.index, StringComparer.Ordinal);

        writer.Write(names.Count);

        foreach (var name in names)
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

            writer.Write(entry.ClassName is { } className ? nameIndexes[className] : NoName);

            writer.Write(entry.Bytes);

            writer.Write(entry.Site is { } site ? nameIndexes[site] : NoName);
        }
    }

    internal static MemoryMap Read(BinaryReader reader, Func<int, CallStackNode?> nodeOf)
    {
        var version = ReadVersion(reader);

        if (version is < FirstVersion or > FormatVersion)
        {
            throw new InvalidDataException("The memory map was exported before declared types and lengths were recorded and must be "
                                           + "re-recorded");
        }

        var names = new string[reader.ReadInt32()];

        for (var index = 0; index < names.Length; index++)
        {
            names[index] = reader.ReadString();
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
                                              ClassName: ReadName(reader, names),
                                              Bytes: reader.ReadUInt64(),
                                              Site: version >= SiteVersion ? ReadName(reader, names) : null);
        }

        return Build(entries, nodeOf);
    }

    private static int ReadVersion(BinaryReader reader) => -reader.ReadInt32();

    private static string? ReadName(BinaryReader reader, string[] names)
    {
        var index = reader.ReadInt32();

        return index == NoName ? null : names[index];
    }
}
