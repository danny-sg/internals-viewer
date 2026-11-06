using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Address;

public sealed partial class AddressMap
{
    private const int FormatVersion = 3;

    private const int FirstVersion = 1;

    private const int SiteVersion = 2;

    private const int MemoryObjectVersion = 3;

    private const int NoName = -1;

    internal static bool IsSupported(BinaryReader reader) => ReadVersion(reader) is >= FirstVersion and <= FormatVersion;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        var names = Labels.SelectMany(l => new[] { l.ClassName, l.Site, l.MemoryObject })
                          .OfType<string>()
                          .Distinct(StringComparer.Ordinal)
                          .ToList();

        var nameIndexes = names.Select((name, index) => (name, index)).ToDictionary(n => n.name, n => n.index, StringComparer.Ordinal);

        writer.Write(names.Count);

        foreach (var name in names)
        {
            writer.Write(name);
        }

        writer.Write(Count);

        foreach (var (address, entries) in Entries)
        {
            foreach (var entry in entries)
            {
                var label = Labels[entry.LabelIndex];

                writer.Write(address);

                writer.Write(entry.CallIndex);

                writer.Write(entry.Owner);

                writer.Write(entry.FromPosition.Sequence);

                writer.Write(entry.FromPosition.Steps);

                writer.Write(entry.ToPosition.Sequence);

                writer.Write(entry.ToPosition.Steps);

                writer.Write(entry.CallNodeIndex);

                writer.Write(entry.Thread);

                writer.Write((byte)label.Operation);

                writer.Write(label.ClassName is { } className ? nameIndexes[className] : NoName);

                writer.Write(entry.Bytes);

                writer.Write(label.Site is { } site ? nameIndexes[site] : NoName);

                writer.Write(label.MemoryObject is { } memoryObject ? nameIndexes[memoryObject] : NoName);
            }
        }
    }

    internal static AddressMap Read(BinaryReader reader, Func<int, CallTreeNode?> nodeOf)
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
                                              Operation: (AddressMapOperation)reader.ReadByte(),
                                              ClassName: ReadName(reader, names),
                                              Bytes: reader.ReadUInt64(),
                                              Site: version >= SiteVersion ? ReadName(reader, names) : null,
                                              MemoryObject: version >= MemoryObjectVersion ? ReadName(reader, names) : null);
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
