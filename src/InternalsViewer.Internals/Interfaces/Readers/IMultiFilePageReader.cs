using System.Threading;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.Readers;

public interface IMultiFilePageReader
{
    Task RegisterFiles(IReadOnlyList<DatabaseFile> files, CancellationToken cancellationToken);
}
