using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

/// <summary>
/// Service responsible for loading CI (Compression Information) data structures
/// </summary>
public class CompressionInfoLoader(CompressedDataRecordLoader compressedDataRecordLoader)
{
    private CompressedDataRecordLoader CompressedDataRecordLoader { get; } = compressedDataRecordLoader;

    public CompressionInfo Load(AllocationUnitPage page, ushort offset)
    {
        var ci = new CompressionInfo();

        ParseHeader(ci, page.Data, offset);

        ci.PageModificationCount = BitConverter.ToInt16(page.Data, offset + 1);

        ci.MarkProperty(nameof(ci.PageModificationCount), offset + sizeof(byte), sizeof(short));

        ci.Length = BitConverter.ToUInt16(page.Data, offset + 3);

        ci.MarkProperty(nameof(ci.Length), offset + sizeof(byte) + sizeof(short), sizeof(short));

        if (ci.HasDictionary)
        {
            ci.Size = BitConverter.ToInt16(page.Data, offset + 5);

            ci.MarkProperty(nameof(ci.Size), offset + sizeof(byte) + sizeof(short) + sizeof(short), sizeof(short));
        }

        if (ci.HasAnchorRecord)
        {
            LoadAnchor(ci, page, offset + (ci.HasDictionary ? 7 : 5));
        }

        if (ci.HasDictionary)
        {
            LoadDictionary(ci, page.Data, (ushort)(offset + ci.Length));
        }

        return ci;
    }

    private void ParseHeader(CompressionInfo ci, byte[] pageData, int offset)
    {
        ci.Header = pageData[offset];

        ci.HasAnchorRecord = (ci.Header & 0b00000010) != 0;
        ci.HasDictionary = (ci.Header & 0b00000100) != 0;

        var tags = new List<string>();

        tags.AddIf("Has Anchor Record", ci.HasAnchorRecord);
        tags.AddIf("Has Dictionary", ci.HasDictionary);

        ci.MarkProperty(nameof(ci.Header), offset, sizeof(byte), tags);
    }

    private static void LoadDictionary(CompressionInfo ci, byte[] data, ushort offset)
    {
        ci.CompressionDictionary = DictionaryLoader.Load(data, offset);

        ci.MarkProperty(nameof(ci.CompressionDictionary), offset, ci.Size);
    }

    private void LoadAnchor(CompressionInfo ci, AllocationUnitPage page, int offset)
    {
        int records = page.Data[offset + 1];

        var structure = CreateTableStructure(records);

        var anchorRecord = CompressedDataRecordLoader.Load(page, (ushort)offset, structure);

        ci.AnchorRecord = anchorRecord;

        ci.MarkProperty(nameof(ci.AnchorRecord), offset, ci.Length - (ci.HasDictionary ? 7 : 5));
    }

    private static TableStructure CreateTableStructure(int records)
    {
        var structure = new TableStructure(-1);

        for (short i = 0; i < records; i++)
        {
            var column = new ColumnStructure();

            column.ColumnName = $"Column {i}";
            column.ColumnId = i;
            column.LeafOffset = i;
            column.DataType = SqlDbType.VarBinary;
            column.DataLength = 8000;

            structure.Columns.Add(column);
        }

        return structure;
    }
}