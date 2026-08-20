using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Interfaces.Readers.Internals;

public interface IRecordReader
{
    Task<List<Record>> Read(DatabaseSource database, 
                            PageAddress startPage, 
                            TableStructure structure,
                            CancellationToken cancellationToken);
}
