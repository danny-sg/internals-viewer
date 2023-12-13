using InternalsViewer.Internals.Interfaces.MetadataProviders;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Services;

/// <summary>
/// Service responsible for getting the Buffer Pool
/// </summary>
/// <remarks>
/// From Books Online:
/// 
///     Buffer pool
///     
///     Also called buffer cache. The buffer pool is a global resource shared by all databases for their cached data pages.
/// </remarks>
public class BufferPoolService(IBufferPoolInfoProvider provider)
{
    public IBufferPoolInfoProvider Provider { get; } = provider;

    public async Task<BufferPool> GetBufferPool(string databaseName)
    {
        var (clean, dirty) = await Provider.GetBufferPoolEntries(databaseName);

        var bufferPool = new BufferPool(clean, dirty);

        return bufferPool;
    }
}
