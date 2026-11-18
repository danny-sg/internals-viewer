using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Tabs;

[Trait("Category", "Unit")]
public class EmbeddedObjectReaderTests
{
    private const ulong Block = 0x1000;

    private const string Container = "CMxcContainer";

    [Fact]
    public void An_Object_Ends_At_The_Last_Byte_Its_Own_Class_Accessed()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Block, 0x100)],
                                                   [(2, Block + 0x59, new byte[5]), (3, Block + 0x60, new byte[8])]);

        var found = Assert.Single(EmbeddedObjectReader.Read([Entry(Block + 0x48, Container)], store, 0, _ => Container));

        Assert.Equal((Block + 0x48, Container, 0x20), (found.Address, found.Type, found.Length));
    }

    [Fact]
    public void Accesses_By_Another_Class_Do_Not_Measure_An_Object()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Block, 0x100)], [(2, Block + 0x60, new byte[8])]);

        Assert.Equal(0, Assert.Single(EmbeddedObjectReader.Read([Entry(Block + 0x48, Container)], store, 0, _ => "CCompPlan")).Length);
    }

    [Fact]
    public void An_Object_Ends_Where_The_Next_Object_Starts()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Block, 0x100)], [(2, Block + 0x48, new byte[0x40])]);

        var found = EmbeddedObjectReader.Read([Entry(Block + 0x48, Container), Entry(Block + 0x58, "CPmoLock")], store, 0, _ => Container);

        Assert.Equal([0x10, 0], found.Select(f => f.Length));
    }

    private static AddressMapEntry Entry(ulong address, string className)
        => new(address, AddressMapOperation.Visited, 0, 0, At(2), TracePosition.Unknown, 7, 0, className, null);
}
