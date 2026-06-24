using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

namespace InternalsViewer.Internals.Services.Records;

/// <summary>
/// Service responsible for loading records from pages
/// </summary>
public sealed class RecordService(FixedVarIndexRecordLoader fixedVarIndexRecordLoader,
                                  FixedVarDataRecordLoader fixedVarDataRecordLoader,
                                  CdDataRecordLoader cdDataRecordLoader,
                                  CdIndexRecordLoader cdIndexRecordLoader) : IRecordService
{
    private FixedVarIndexRecordLoader FixedVarIndexRecordLoader { get; } = fixedVarIndexRecordLoader;

    private FixedVarDataRecordLoader FixedVarDataRecordLoader { get; } = fixedVarDataRecordLoader;

    private CdDataRecordLoader CdDataRecordLoader { get; } = cdDataRecordLoader;

    private CdIndexRecordLoader CdIndexRecordLoader { get; } = cdIndexRecordLoader;

    public IEnumerable<IRecord> GetRecords(AllocationUnitPage page)
    {
        var isCompressed = page.AllocationUnit.CompressionType != CompressionType.None;

        return page switch
        {
            DataPage dataPage when !isCompressed 
                => GetFixedVarDataRecords(dataPage),
            DataPage dataPage 
                => GetCdDataRecords(dataPage),
            IndexPage indexPage when !isCompressed 
                => GetIndexRecords(indexPage),
            IndexPage indexPage 
                => GetCdIndexRecords(indexPage),
            _ => throw new InvalidOperationException("Unknown page type")
        };
    }

    public IEnumerable<IRecord> GetDataRecords(DataPage page)
    {
        var isCompressed = page.AllocationUnit.CompressionType != CompressionType.None;

        if (isCompressed)
        {
            return GetCdDataRecords(page);
        }

        return GetFixedVarDataRecords(page);
    }

    public IEnumerable<IIndexRecord> GetIndexRecords(IndexPage page)
    {
        var isCompressed = page.AllocationUnit.CompressionType != CompressionType.None;

        if(isCompressed)
        {
            return GetCdIndexRecords(page);
        }

        return GetFixedVarIndexRecords(page);
    }

    private IEnumerable<DataRecord> GetFixedVarDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select((s, index) =>
        {
            var record = FixedVarDataRecordLoader.Load(page, s, structure);

            record.Slot = index;

            return record;
        }).ToList();
    }

    private IEnumerable<CdRecord> GetCdDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
                   .Select((s, index) =>
                    {
                        var record = CdDataRecordLoader.Load(page, s, structure);

                        record.Slot = index;

                        return record;
                    })
                   .ToList();
    }

    private IEnumerable<FixedVarIndexRecord> GetFixedVarIndexRecords(IndexPage page)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
                   .Select((s, index) => FixedVarIndexRecordLoader.Load(page, s, index, structure))
                    .ToList();
    }

    private IEnumerable<CdIndexRecord> GetCdIndexRecords(IndexPage page)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
            .Select((s, index) =>
            {
                var record = CdIndexRecordLoader.Load(page, s, structure);

                record.Slot = index;

                return record;
            })
            .ToList();
    }
}