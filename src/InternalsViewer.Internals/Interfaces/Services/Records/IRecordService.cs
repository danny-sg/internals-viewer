using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Interfaces.Services.Records;

public interface IRecordService
{
    IEnumerable<IRecord> GetRecords(AllocationUnitPage page);

    IEnumerable<IRecord> GetDataRecords(DataPage page);

    IEnumerable<IIndexRecord> GetIndexRecords(IndexPage page);
}