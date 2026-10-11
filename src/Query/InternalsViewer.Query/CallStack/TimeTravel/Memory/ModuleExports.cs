using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

internal static class ModuleExports
{
    private const int NameCountOffset = 24;

    private const int FunctionsOffset = 28;

    private const int NamesOffset = 32;

    private const int OrdinalsOffset = 36;

    private const uint OrdinalSize = sizeof(ushort);

    private const uint EntrySize = sizeof(uint);

    public static List<(string Name, uint Rva)> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(path);

            using var reader = new PEReader(stream);

            if (reader.PEHeaders.PEHeader?.ExportTableDirectory is not { Size: > 0 } directory)
            {
                return [];
            }

            var start = (uint)directory.RelativeVirtualAddress;

            var end = start + (uint)directory.Size;

            var names = ReadUInt32(reader, start + NameCountOffset);

            var functions = ReadUInt32(reader, start + FunctionsOffset);

            var nameTable = ReadUInt32(reader, start + NamesOffset);

            var ordinals = ReadUInt32(reader, start + OrdinalsOffset);

            var exports = new List<(string Name, uint Rva)>((int)names);

            for (uint index = 0; index < names; index++)
            {
                var ordinal = ReadUInt16(reader, ordinals + index * OrdinalSize);

                var rva = ReadUInt32(reader, functions + ordinal * EntrySize);

                if (rva >= start && rva < end)
                {
                    continue;
                }

                exports.Add((ReadName(reader, ReadUInt32(reader, nameTable + index * EntrySize)), rva));
            }

            return exports;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return [];
        }
    }

    private static uint ReadUInt32(PEReader reader, uint rva) => reader.GetSectionData((int)rva).GetReader().ReadUInt32();

    private static ushort ReadUInt16(PEReader reader, uint rva) => reader.GetSectionData((int)rva).GetReader().ReadUInt16();

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
