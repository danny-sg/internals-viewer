using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Interfaces.Services.Records;

public interface IRecordService
{
    IEnumerable<IRecord> GetRecords(Page page, bool isMarkEnabled = false);

    IEnumerable<IRecord> GetDataRecords(DataPage page, bool isMarkEnabled = false);

    IEnumerable<IIndexRecord> GetIndexRecords(IndexPage page, bool isMarkEnabled = false);
}