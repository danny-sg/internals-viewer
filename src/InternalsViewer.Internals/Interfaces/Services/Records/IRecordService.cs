using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Interfaces.Services.Records;

public interface IRecordService
{
    IEnumerable<IRecord> GetRecords(Page page);

    IEnumerable<IRecord> GetDataRecords(DataPage page);

    IEnumerable<IIndexRecord> GetIndexRecords(IndexPage page);

    IRecord GetDataRecord(DataPage page, int slot, TableStructure structure);

    IIndexRecord GetIndexRecord(IndexPage page, int slot, IndexStructure structure);
}