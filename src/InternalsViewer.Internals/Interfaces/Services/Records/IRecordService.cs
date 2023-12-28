using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;

namespace InternalsViewer.Internals.Interfaces.Services.Records;

public interface IRecordService
{
    DataRecord GetDataRecord(Page page, ushort offset);

    IndexRecord GetIndexRecord(Page page, ushort offset);
}