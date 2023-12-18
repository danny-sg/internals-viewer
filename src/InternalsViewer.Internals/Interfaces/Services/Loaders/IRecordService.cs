using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IRecordService
{
    Task<DataRecord> GetDataRecord(Page page, ushort offset);

    Task<IndexRecord> GetIndexRecord(Page page, ushort offset);
}