using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Interfaces.Readers.Internals;

public interface IRecordReader
{
    Task<List<DataRecord>> Read(DatabaseDetail database, PageAddress startPage, TableStructure structure);
}
