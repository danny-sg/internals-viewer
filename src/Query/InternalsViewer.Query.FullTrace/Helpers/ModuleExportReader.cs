using System.Reflection.PortableExecutable;
using System.Text;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal static class ModuleExportReader
{
    private const int NameCountOffset = 24;

    private const int FunctionsOffset = 28;

    private const int NamesOffset = 32;

    private const int OrdinalsOffset = 36;

    private const uint OrdinalSize = sizeof(ushort);

    private const uint EntrySize = sizeof(uint);

    private const byte JumpOpcode = 0xE9;

    private const int JumpLength = 5;

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

            var named = exports.Select(e => e.Rva).ToHashSet();

            var imageSize = (uint)(reader.PEHeaders.PEHeader?.SizeOfImage ?? 0);

            foreach (var (name, rva) in exports.ToList())
            {
                if (GetJumpTarget(reader, rva, imageSize) is { } target && named.Add(target))
                {
                    exports.Add((name, target));
                }
            }

            return exports;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return [];
        }
    }

    private static uint? GetJumpTarget(PEReader reader, uint rva, uint imageSize)
    {
        var code = reader.GetSectionData((int)rva).GetReader();

        if (code.RemainingBytes < JumpLength || code.ReadByte() != JumpOpcode)
        {
            return null;
        }

        var target = (long)rva + JumpLength + code.ReadInt32();

        return target > 0 && target < imageSize ? (uint)target : null;
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
