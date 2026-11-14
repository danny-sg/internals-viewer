using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Address;

[Trait("Category", "Unit")]
public class ObjectClassResolverTests
{
    private const ulong Object = 0x21C_0BB4_C180;

    private const ulong BaseVtable = 0x7FF8_0090_0000;

    private const ulong DerivedVtable = 0x7FF8_0090_0100;

    private static readonly Dictionary<ulong, string> Classes = new()
    {
        [BaseVtable] = "CValRefOrdinal",
        [DerivedVtable] = "CValRefPrefetch"
    };

    [Fact]
    public void An_Object_Is_The_Class_It_Was_Built_Up_To_Before_It_Was_Torn_Down()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68, from: 1, to: 100)],
                                                   [(2, Object, BitConverter.GetBytes(BaseVtable)),
                                                    (3, Object, BitConverter.GetBytes(DerivedVtable)),
                                                    (10, Object, BitConverter.GetBytes(BaseVtable))]);

        var found = ObjectClassResolver.GetClass(store, Classes, 0, []);

        Assert.Equal(("CValRefPrefetch", 2), (found.ClassName, found.VtableCount));
    }

    [Fact]
    public void An_Object_Is_The_Class_It_Holds_While_Its_Members_Are_Called()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68, from: 1, to: 100)],
                                                   [(2, Object, BitConverter.GetBytes(DerivedVtable)),
                                                    (3, Object, BitConverter.GetBytes(BaseVtable)),
                                                    (10, Object, BitConverter.GetBytes(DerivedVtable)),
                                                    (20, Object, BitConverter.GetBytes(BaseVtable))]);

        Assert.Equal("CValRefPrefetch", ObjectClassResolver.GetClass(store, Classes, 0, [At(12), At(15)]).ClassName);
    }

    [Fact]
    public void An_Object_Never_Freed_Keeps_The_Last_Vtable_It_Was_Given()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68)],
                                                   [(2, Object, BitConverter.GetBytes(BaseVtable)),
                                                    (3, Object, BitConverter.GetBytes(DerivedVtable))]);

        Assert.Equal("CValRefPrefetch", ObjectClassResolver.GetClass(store, Classes, 0, []).ClassName);
    }

    [Fact]
    public void Memory_That_Never_Holds_A_Vtable_Has_No_Class()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68)],
                                                   [(2, Object, BitConverter.GetBytes(0x1234ul))]);

        Assert.Null(ObjectClassResolver.GetClass(store, Classes, 0, []).ClassName);
    }

    [Fact]
    public void The_Map_Takes_The_Class_Of_The_Object_Each_Entry_Holds()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68)],
                                                   [(2, Object, BitConverter.GetBytes(BaseVtable)),
                                                    (3, Object, BitConverter.GetBytes(DerivedVtable))]);

        var map = AddressMap.Build([new AddressEntry(Object, 1, 0, 0, At(1), TracePosition.Unknown, -1, 9, AddressMapOperation.Allocated)],
                                   _ => null);

        var resolved = ObjectClassResolver.Resolve(map, store, Classes);

        Assert.Equal("CValRefPrefetch", resolved.GetVtableClassName(Object, At(0), At(100)));
    }

    [Fact]
    public void A_Constructed_Object_Holding_One_Vtable_Keeps_Its_Constructors_Class()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68)],
                                                   [(2, Object, BitConverter.GetBytes(BaseVtable))]);

        var map = AddressMap.Build([Constructed("CPvrBitSet")], _ => null);

        Assert.Null(ObjectClassResolver.Resolve(map, store, Classes).GetVtableClassName(Object, At(0), At(100)));
    }

    [Fact]
    public void A_Constructed_Object_Whose_Vtable_Changed_Takes_The_Class_It_Became()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68)],
                                                   [(2, Object, BitConverter.GetBytes(BaseVtable)),
                                                    (3, Object, BitConverter.GetBytes(DerivedVtable))]);

        var map = AddressMap.Build([Constructed("CValRefOrdinal")], _ => null);

        Assert.Equal("CValRefPrefetch", ObjectClassResolver.Resolve(map, store, Classes).GetVtableClassName(Object, At(0), At(100)));
    }

    [Fact]
    public void A_Named_Object_Does_Not_Take_The_Base_Its_Destructor_Left_Behind()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68, from: 1, to: 100)],
                                                   [(2, Object, BitConverter.GetBytes(DerivedVtable)),
                                                    (3, Object, BitConverter.GetBytes(BaseVtable)),
                                                    (4, Object, BitConverter.GetBytes(DerivedVtable)),
                                                    (10, Object, BitConverter.GetBytes(BaseVtable))]);

        var map = AddressMap.Build([Constructed("CValRefPrefetch") with { Operation = AddressMapOperation.Visited }], _ => null);

        Assert.Null(ObjectClassResolver.Resolve(map, store, Classes).GetVtableClassName(Object, At(0), At(100)));
    }

    [Fact]
    public void A_Constructed_Class_Without_A_Vtable_Of_Its_Own_Is_Not_Named_After_Its_First_Member()
    {
        var store = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Object, 0x68)],
                                                   [(2, Object, BitConverter.GetBytes(BaseVtable)),
                                                    (3, Object, BitConverter.GetBytes(DerivedVtable))]);

        var map = AddressMap.Build([Constructed("HoBtStatistics")], _ => null);

        Assert.Null(ObjectClassResolver.Resolve(map, store, Classes).GetVtableClassName(Object, At(0), At(100)));
    }

    private static AddressEntry Constructed(string className)
        => new(Object, 1, 0, 0, At(1), TracePosition.Unknown, -1, 9, AddressMapOperation.Constructed, className);
}
