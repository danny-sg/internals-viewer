using System.Collections;
using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

/// <summary>
/// Service responsible for loading CI (Compression Information) data structures
/// </summary>
/// <remarks>
/// 
/// </remarks>
public class CompressionInfoLoader(CompressedDataRecordLoader compressedDataRecordLoader)
{
    private CompressedDataRecordLoader CompressedDataRecordLoader { get; } = compressedDataRecordLoader;

    private static readonly short Offset = 96;

    public CompressionInfo Load(AllocationUnitPage page)
    {
        var ci = new CompressionInfo();

        ci.HeaderBits = new BitArray(new[] { page.Data[CompressionInfo.SlotOffset] });

        ci.MarkProperty("Header", Offset, 1);

        ci.HasAnchorRecord = ci.HeaderBits[1];
        ci.HasDictionary = ci.HeaderBits[2];

        ci.PageModificationCount = BitConverter.ToInt16(page.Data, CompressionInfo.SlotOffset + 1);

        ci.MarkProperty("PageModificationCount", CompressionInfo.SlotOffset + sizeof(byte), sizeof(short));

        ci.Length = BitConverter.ToInt16(page.Data, CompressionInfo.SlotOffset + 3);

        ci.MarkProperty("Length", CompressionInfo.SlotOffset + sizeof(byte) + sizeof(short), sizeof(short));

        if (ci.HasDictionary)
        {
            ci.Size = BitConverter.ToInt16(page.Data, CompressionInfo.SlotOffset + 5);

            ci.MarkProperty("Size", CompressionInfo.SlotOffset + sizeof(byte) + sizeof(short) + sizeof(short), sizeof(short));
        }

        if (ci.HasAnchorRecord)
        {
            ci.MarkProperty("AnchorRecord");
            
            LoadAnchor(ci, page);
        }

        if (ci.HasDictionary)
        {
            ci.MarkProperty("CompressionDictionary");

            LoadDictionary(ci, page);
        }

        return ci;
    }

    private static void LoadDictionary(CompressionInfo ci, AllocationUnitPage page)
    {
        ci.CompressionDictionary = DictionaryLoader.Load(page.Data, Offset + ci.Length);
    }

    private void LoadAnchor(CompressionInfo ci, AllocationUnitPage page)
    {
        var startOffset = (ci.HasDictionary ? 7 : 5) + CompressionInfo.SlotOffset;

        int records = page.Data[startOffset + 1];

        var structure = CreateTableStructure(records, ci, page);

        var anchorRecord = CompressedDataRecordLoader.Load(page, (ushort)startOffset, structure);

        ci.AnchorRecord = anchorRecord;
    }

    private static TableStructure CreateTableStructure(int records, CompressionInfo ci, Page page)
    {
        var structure = new TableStructure(page.PageHeader.AllocationUnitId);

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