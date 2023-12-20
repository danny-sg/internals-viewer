using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Interfaces.Readers.Internals;

public interface ITableReader
{
    Task<List<DataRecord>> Read(DatabaseDetail databaseDetail, PageAddress startPage, TableStructure structure);
}
