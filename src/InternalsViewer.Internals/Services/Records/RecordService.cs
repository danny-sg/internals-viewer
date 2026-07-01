using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

namespace InternalsViewer.Internals.Services.Records;

/// <summary>
/// Service responsible for loading records from pages
/// </summary>
public sealed class RecordService(FixedVarIndexRecordLoader fixedVarIndexRecordLoader,
                                  FixedVarDataRecordLoader fixedVarDataRecordLoader,
                                  CdDataRecordLoader cdDataRecordLoader,
                                  CdIndexRecordLoader cdIndexRecordLoader, 
                                  LobRecordLoader lobRecordLoader) : IRecordService
{
    private FixedVarIndexRecordLoader FixedVarIndexRecordLoader { get; } = fixedVarIndexRecordLoader;

    private FixedVarDataRecordLoader FixedVarDataRecordLoader { get; } = fixedVarDataRecordLoader;

    private CdDataRecordLoader CdDataRecordLoader { get; } = cdDataRecordLoader;

    private CdIndexRecordLoader CdIndexRecordLoader { get; } = cdIndexRecordLoader;

    private LobRecordLoader LobRecordLoader { get; } = lobRecordLoader;

    public IEnumerable<IRecord> GetRecords(Page page, bool isMarkEnabled = false)
    {
        var isCompressed = page is AllocationUnitPage allocationPage 
                           && allocationPage.AllocationUnit.CompressionType != CompressionType.None;

        return page switch
        {
            DataPage dataPage when !isCompressed
                => GetFixedVarDataRecords(dataPage, isMarkEnabled),
            DataPage dataPage
                => GetCdDataRecords(dataPage, isMarkEnabled),
            IndexPage indexPage when !isCompressed
                => GetIndexRecords(indexPage, isMarkEnabled),
            IndexPage indexPage
                => GetCdIndexRecords(indexPage, isMarkEnabled),
            LobPage lobPage 
                => GetLobRecords(lobPage, isMarkEnabled),
            _ => throw new InvalidOperationException("Unknown page type")
        };
    }

    public IEnumerable<IRecord> GetDataRecords(DataPage page, bool isMarkEnabled = false)
    {
        var isCompressed = page.AllocationUnit.CompressionType != CompressionType.None;

        if (isCompressed)
        {
            return GetCdDataRecords(page, isMarkEnabled);
        }

        return GetFixedVarDataRecords(page, isMarkEnabled);
    }

    public IEnumerable<IIndexRecord> GetIndexRecords(IndexPage page, bool isMarkEnabled = false)
    {
        var isCompressed = page.AllocationUnit.CompressionType != CompressionType.None;

        if (isCompressed)
        {
            return GetCdIndexRecords(page, isMarkEnabled);
        }

        return GetFixedVarIndexRecords(page, isMarkEnabled);
    }

    private IEnumerable<IRecord> GetLobRecords(LobPage page, bool isMarkEnabled)
    {
        return page.OffsetTable.Select((s, index) =>
        {
            var record = LobRecordLoader.Load(page, s, isMarkEnabled);

            record.Slot = index;

            return record;
        }).ToList();
    }

    private IEnumerable<DataRecord> GetFixedVarDataRecords(DataPage page, bool isMarkEnabled = false)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select((s, index) =>
        {
            var record = FixedVarDataRecordLoader.Load(page, s, structure, isMarkEnabled);

            record.Slot = index;

            return record;
        }).ToList();
    }

    private IEnumerable<CdRecord> GetCdDataRecords(DataPage page, bool isMarkEnabled = false)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
                   .Select((s, index) =>
                    {
                        var record = CdDataRecordLoader.Load(page, s, structure, isMarkEnabled);

                        record.Slot = index;

                        return record;
                    })
                   .ToList();
    }

    private IEnumerable<FixedVarIndexRecord> GetFixedVarIndexRecords(IndexPage page, bool isMarkEnabled = false)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
                   .Select((s, index) => FixedVarIndexRecordLoader.Load(page, s, index, structure, isMarkEnabled))
                   .ToList();
    }

    private IEnumerable<CdIndexRecord> GetCdIndexRecords(IndexPage page, bool isMarkEnabled = false)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
                   .Select((s, index) =>
                   {
                       var record = CdIndexRecordLoader.Load(page, s, structure, isMarkEnabled);
                   
                       record.Slot = index;
                   
                       return record;
                   })
                   .ToList();
    }
}