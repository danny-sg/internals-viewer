using System.Reflection.PortableExecutable;
using System.Text;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal static class ClassHierarchyReader
{
    private const uint BaseCountOffset = 8;

    private const uint BaseArrayOffset = 12;

    private const uint TypeNameOffset = 16;

    private const uint EntrySize = sizeof(uint);

    private const uint MaximumBases = 256;

    private const string ClassPrefix = ".?AV";

    private const string StructPrefix = ".?AU";

    private const string NameSuffix = "@@";

    public static Dictionary<string, string[]> Read(string path, IReadOnlyDictionary<string, uint> hierarchies)
    {
        var bases = new Dictionary<string, string[]>();

        if (!File.Exists(path))
        {
            return bases;
        }

        try
        {
            using var stream = File.OpenRead(path);

            using var reader = new PEReader(stream);

            foreach (var (className, hierarchy) in hierarchies)
            {
                if (ReadBases(reader, hierarchy) is { } names)
                {
                    bases[className] = names;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
        }

        return bases;
    }

    private static string[]? ReadBases(PEReader reader, uint hierarchy)
    {
        try
        {
            var count = ReadUInt32(reader, hierarchy + BaseCountOffset);

            if (count > MaximumBases)
            {
                return null;
            }

            var array = ReadUInt32(reader, hierarchy + BaseArrayOffset);

            var names = new string[count];

            for (uint index = 0; index < count; index++)
            {
                var descriptor = ReadUInt32(reader, array + index * EntrySize);

                names[index] = GetTypeName(ReadName(reader, ReadUInt32(reader, descriptor) + TypeNameOffset));
            }

            return names;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string GetTypeName(string decorated)
        => (decorated.StartsWith(ClassPrefix, StringComparison.Ordinal) || decorated.StartsWith(StructPrefix, StringComparison.Ordinal))
           && decorated.EndsWith(NameSuffix, StringComparison.Ordinal)
            ? decorated[ClassPrefix.Length..^NameSuffix.Length]
            : decorated;

    private static uint ReadUInt32(PEReader reader, uint rva) => reader.GetSectionData((int)rva).GetReader().ReadUInt32();

    private static string ReadName(PEReader reader, uint rva)
    {
        var blob = reader.GetSectionData((int)rva).GetReader();

        var name = new StringBuilder();

        while (blob.RemainingBytes > 0 && blob.ReadByte() is var character and not 0)
        {
            name.Append((char)character);
        }

        return name.ToString();
    }
}
