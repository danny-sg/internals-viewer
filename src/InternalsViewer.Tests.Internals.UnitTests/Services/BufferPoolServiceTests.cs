using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Services;
using Moq;

namespace InternalsViewer.Tests.Internals.UnitTests.Services;

public class BufferPoolServiceTests
{
    [Fact]
    public async Task Can_Get_Buffer_Pool()
    {
        var provider = new Mock<IBufferPoolInfoProvider>();

        var results = 
            (
                Clean: new List<PageAddress> { new(1, 100), new(1, 102) },
                Dirty: new List<PageAddress> { new(1, 200), new(1, 202) }
            );

        provider.Setup(p => p.GetBufferPoolEntries("TestDatabase"))
                .ReturnsAsync(results);

        var service = new BufferPoolService(provider.Object);

        var bufferPool = await service.GetBufferPool("TestDatabase");

        Assert.Equivalent(results.Clean, bufferPool.CleanPages);
        Assert.Equivalent(results.Dirty, bufferPool.DirtyPages);
    }
}
