using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IBufferPoolInfoProvider
{
    Task<(List<PageAddress> Clean, List<PageAddress> Dirty)> GetBufferPoolEntries(string databaseName);
}