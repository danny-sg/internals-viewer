using System.Collections;
using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

/// <summary>
/// Service responsible for loading CI (Compression Information) data structures
/// </summary>
public class CompressionInfoService(IDictionaryService dictionaryService,
                                    IStructureInfoProvider structureInfoProvider,
                                    ICompressedDataRecordService compressedDataRecordService)
    : ICompressionInfoService
{
    public IDictionaryService DictionaryService { get; } = dictionaryService;

    public IStructureInfoProvider StructureInfoProvider { get; } = structureInfoProvider;

    public ICompressedDataRecordService CompressedDataRecordService { get; } = compressedDataRecordService;                                

    public static byte CiSize = 7;
    public static short Offset = 96;

    public CompressionInfo Load(AllocationUnitPage page)
    {
        var ci = new CompressionInfo();

        ci.StatusBits = new BitArray(new[] { page.Data[ci.SlotOffset] });

        ci.MarkDataStructure("StatusDescription", ci.SlotOffset, 1);

        ci.HasAnchorRecord = ci.StatusBits[1];
        ci.HasDictionary = ci.StatusBits[2];

        ci.PageModCount = BitConverter.ToInt16(page.Data, ci.SlotOffset + 1);

        ci.MarkDataStructure("PageModCount", ci.SlotOffset + sizeof(byte), sizeof(short));

        ci.Length = BitConverter.ToInt16(page.Data, ci.SlotOffset + 3);

        ci.MarkDataStructure("Length", ci.SlotOffset + sizeof(byte) + sizeof(short), sizeof(short));

        if (ci.HasDictionary)
        {
            ci.Size = BitConverter.ToInt16(page.Data, ci.SlotOffset + 5);

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

    private void LoadDictionary(CompressionInfo ci, AllocationUnitPage page)
    {
        ci.CompressionDictionary = DictionaryService.Load(page.Data, Offset + ci.Length);
    }

    private void LoadAnchor(CompressionInfo ci, AllocationUnitPage page)
    {
        var startOffset = (ci.HasDictionary ? 7 : 5) + ci.SlotOffset;

        int records = page.Data[startOffset + 1];

        var structure = CreateTableStructure(records, ci, page);

        var anchorRecord = CompressedDataRecordService.Load(page, (ushort)startOffset, structure);

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

    public async Task<CompressionInfo?> GetCompressionInfo(Page page)
    {
        var compressionType = await StructureInfoProvider.GetCompressionType(page.PageHeader.AllocationUnitId);

        if (compressionType == CompressionType.None)
        {
            return null;
        }

        return await GetCompressionInfo(page);
    }
}