using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Interfaces.Readers.Internals;

public interface IRecordReader
{
    Task<List<DataRecord>> Read(DatabaseSource database, PageAddress startPage, TableStructure structure);
}
