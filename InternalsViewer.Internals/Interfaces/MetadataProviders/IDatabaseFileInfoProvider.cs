using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IDatabaseFileInfoProvider
{
    Task<List<DatabaseFile>> GetFiles(string name);

    Task<int> GetFileSize(short fileId);
}