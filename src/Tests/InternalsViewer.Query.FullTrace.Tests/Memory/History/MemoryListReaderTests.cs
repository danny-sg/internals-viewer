using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryListReaderTests
{
    private const ulong Head = 0x1000;

    private const ulong Tail = 0x2000;

    private const ulong Item = 0x3000;

    private const string Site = "qetempl.h:1116";

    [Fact]
    public void A_Block_Pointing_To_Another_From_The_Same_Site_Is_A_List_Node_Named_By_Its_Item()
    {
        var (store, map) = Build(Pointer(Item), Pointer(Item));

        var node = new MemoryListReader(store, map).GetNode(MemoryHistoryStoreHelper.GetBlock(store, Head));

        Assert.Equal(new MemoryListNode(8, 0, "CAccessorRelease"), node);
    }

    [Fact]
    public void The_Last_Node_Is_A_List_Node_Though_Its_Next_Is_Null()
    {
        var (store, map) = Build(Pointer(Item), Pointer(Item));

        Assert.Equal(8, new MemoryListReader(store, map).GetNode(MemoryHistoryStoreHelper.GetBlock(store, Tail))?.NextOffset);
    }

    [Fact]
    public void Blocks_That_Only_Point_Elsewhere_Are_Not_A_List()
    {
        var (store, map) = Build(Pointer(Item), Pointer(Item), isLinked: false);

        Assert.Null(new MemoryListReader(store, map).GetNode(MemoryHistoryStoreHelper.GetBlock(store, Head)));
    }

    private static (MemoryHistoryStore Store, AddressMap Map) Build(byte[] headItem, byte[] tailItem, bool isLinked = true)
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Head, 16),
                                                    MemoryHistoryStoreHelper.Block(Tail, 16),
                                                    MemoryHistoryStoreHelper.Block(Item, 40)],
                                                   [(2, Head, headItem),
                                                    (2, Head + 8, Pointer(isLinked ? Tail : 0)),
                                                    (3, Tail, tailItem),
                                                    (3, Tail + 8, Pointer(0)),
                                                    (4, Item, [1])]);

        var map = AddressMap.Build([Allocation(Head, 16, null, Site),
                                    Allocation(Tail, 16, null, Site),
                                    Allocation(Item, 40, "CAccessorRelease", null)],
                                   _ => null);

        return (store, map);
    }

    private static AddressEntry Allocation(ulong address, ulong bytes, string? className, string? site)
        => new(address, 1, 0, 0, At(1), TracePosition.Unknown, 0, 7, AddressMapOperation.Allocated, className, bytes, site, null);

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);
}
