using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;

namespace InternalsViewer.Internals.Interfaces.Services.Records;

public interface IRecordService
{
    List<DataRecord> GetDataRecords(DataPage page);

    List<CompressedDataRecord> GetCompressedDataRecords(DataPage page);

    List<IndexRecord> GetIndexRecords(IndexPage page);
}