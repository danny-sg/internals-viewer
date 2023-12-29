using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;

namespace InternalsViewer.Internals.Interfaces.Services.Records;

public interface IRecordService
{
    DataRecord GetDataRecord(DataPage page, ushort offset);

    IndexRecord GetIndexRecord(IndexPage page, ushort offset);
}