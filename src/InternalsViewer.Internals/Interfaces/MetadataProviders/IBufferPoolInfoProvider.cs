using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IBufferPoolInfoProvider
{
    Task<(List<PageAddress> Clean, List<PageAddress> Dirty)> GetBufferPoolEntries(DatabaseSource database);
}