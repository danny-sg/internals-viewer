using System;
using System.Collections;
using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

/// <summary>
/// Service responsible for loading CI (Compression Information) data structures
/// </summary>
public class CompressionInfoService(IDictionaryService dictionaryService,
                                    ICompressedDataRecordService compressedDataRecordService)
    : ICompressionInfoService
{
    public IDictionaryService DictionaryService { get; } = dictionaryService;

    public ICompressedDataRecordService CompressedDataRecordService { get; } = compressedDataRecordService;                                

    public static byte CiSize = 7;
    public static short Offset = 96;

    public CompressionInfo Load(Page page)
    {
        var ci = new CompressionInfo();

        ci.StatusBits = new BitArray(new[] { page.PageData[ci.SlotOffset] });

        ci.MarkDataStructure("StatusDescription", ci.SlotOffset, 1);

        ci.HasAnchorRecord = ci.StatusBits[1];
        ci.HasDictionary = ci.StatusBits[2];

        ci.PageModCount = BitConverter.ToInt16(page.PageData, ci.SlotOffset + 1);

        ci.MarkDataStructure("PageModCount", ci.SlotOffset + sizeof(byte), sizeof(short));

        ci.Length = BitConverter.ToInt16(page.PageData, ci.SlotOffset + 3);

        ci.MarkDataStructure("Length", ci.SlotOffset + sizeof(byte) + sizeof(short), sizeof(short));

        if (ci.HasDictionary)
        {
            ci.Size = BitConverter.ToInt16(page.PageData, ci.SlotOffset + 5);

            ci.MarkDataStructure("Size", ci.SlotOffset + sizeof(byte) + sizeof(short) + sizeof(short), sizeof(short));
        }

        if (ci.HasAnchorRecord)
        {
            LoadAnchor(ci, page);
        }

        if (ci.HasDictionary)
        {
            LoadDictionary(ci, page);
        }

        return ci;
    }

    private void LoadDictionary(CompressionInfo ci, Page page)
    {
        ci.CompressionDictionary = DictionaryService.Load(page.PageData, Offset + ci.Length);
    }

    private void LoadAnchor(CompressionInfo ci, Page page)
    {
        var startOffset = (ci.HasDictionary ? 7 : 5) + ci.SlotOffset;

        int records = page.PageData[startOffset + 1];

        var structure = CreateTableStructure(records, ci, page);

        var anchorRecord = CompressedDataRecordService.Load(page, (ushort)startOffset, structure);

        ci.AnchorRecord = anchorRecord;
    }

    private static TableStructure CreateTableStructure(int records, CompressionInfo ci, Page page)
    {
        var structure = new TableStructure(page.PageHeader.AllocationUnitId);

        for (short i = 0; i < records; i++)
        {
            var column = new Column();

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