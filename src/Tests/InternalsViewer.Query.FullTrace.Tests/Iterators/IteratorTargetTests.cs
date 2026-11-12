using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators;

[Trait("Category", "Unit")]
public class IteratorTargetTests
{
    private const ulong Iterator = 0x10000;

    private const ulong Buffer = 0x20000;

    private const ulong Other = 0x30000;

    [Fact]
    public void A_Row_Buffer_Belongs_To_The_Iterator_Whose_Row_Buffer_Field_Points_At_It()
    {
        var target = new IteratorTarget(new CallTreeNode(), null);

        Assert.Same(target, IteratorTarget.FindRowBufferOwner(new Dictionary<ulong, IteratorTarget> { [Iterator] = target },
                                                              Build(),
                                                              Buffer,
                                                              At(5)));
    }

    [Fact]
    public void A_Buffer_No_Iterator_Points_At_Has_No_Owner()
    {
        var target = new IteratorTarget(new CallTreeNode(), null);

        Assert.Null(IteratorTarget.FindRowBufferOwner(new Dictionary<ulong, IteratorTarget> { [Iterator] = target },
                                                      Build(),
                                                      Other,
                                                      At(5)));
    }

    private static MemoryHistoryStore Build()
        => MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Iterator, 0x238),
                                           MemoryHistoryStoreHelper.Block(Buffer, 0xC0),
                                           MemoryHistoryStoreHelper.Block(Other, 0xC0)],
                                          [(1, Iterator + 0x80, BitConverter.GetBytes(Buffer)),
                                           (1, Buffer, [1]),
                                           (1, Other, [1])]);
}
