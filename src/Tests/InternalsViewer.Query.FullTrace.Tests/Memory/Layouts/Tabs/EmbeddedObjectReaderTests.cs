using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Tabs;

[Trait("Category", "Unit")]
public class EmbeddedObjectReaderTests
{
    private const ulong Block = 0x1000;

    private const ulong Vtable = 0x7FF9_8A1B_0000;

    private const ulong BaseVtable = 0x7FF9_8A1B_0100;

    private const string Container = "CMxcContainer";

    private const string Hash = "CQScanHash";

    [Fact]
    public void An_Object_Ends_At_The_Last_Byte_Its_Own_Class_Accessed()
    {
        var store = Build([(2, Block + 0x59, new byte[5]), (3, Block + 0x60, new byte[8])]);

        var found = Assert.Single(EmbeddedObjectReader.Measure([Embedded(0x48, Container)], store, 0, _ => Container));

        Assert.Equal((Block + 0x48, Container, 0x20), (found.Address, found.Type, found.Length));
    }

    [Fact]
    public void Accesses_By_Another_Class_Do_Not_Measure_An_Object()
    {
        var store = Build([(2, Block + 0x60, new byte[8])]);

        Assert.Equal(0, Assert.Single(EmbeddedObjectReader.Measure([Embedded(0x48, Container)], store, 0, _ => "CCompPlan")).Length);
    }

    [Fact]
    public void An_Object_Ends_Where_The_Next_Object_Starts()
    {
        var store = Build([(2, Block + 0x48, new byte[0x40])]);

        var found = EmbeddedObjectReader.Measure([Embedded(0x48, Container), Embedded(0x58, "CPmoLock")], store, 0, _ => Container);

        Assert.Equal([0x10, 0], found.Select(f => f.Length));
    }

    [Fact]
    public void A_Vtable_Inside_A_Block_Starts_An_Object_Of_Its_Class()
    {
        var store = Build([(2, Block + 0x40, BitConverter.GetBytes(Vtable))]);

        Assert.Equal([Embedded(0x40, Hash)], EmbeddedObjectReader.FindVtableObjects(store, Vtables(), 0, At(5)));
    }

    [Fact]
    public void Another_Vtable_Of_The_Class_Already_Started_Is_A_Base_Part_Not_An_Object()
    {
        var store = Build([(2, Block, BitConverter.GetBytes(Vtable)), (3, Block + 0x10, BitConverter.GetBytes(BaseVtable))]);

        var baseParts = new HashSet<ulong>();

        Assert.Empty(EmbeddedObjectReader.FindVtableObjects(store, Vtables(), 0, At(5), baseParts));

        Assert.Equal([Block + 0x10], baseParts);
    }

    [Fact]
    public void A_Vtable_Repeated_Through_A_Block_Starts_An_Object_Each_Time()
    {
        var store = Build([(2, Block, BitConverter.GetBytes(Vtable)),
                           (3, Block + 0x20, BitConverter.GetBytes(Vtable)),
                           (4, Block + 0x40, BitConverter.GetBytes(Vtable))]);

        Assert.Equal([Embedded(0x20, Hash), Embedded(0x40, Hash)], EmbeddedObjectReader.FindVtableObjects(store, Vtables(), 0, At(5)));
    }

    private static Dictionary<ulong, string> Vtables() => new() { [Vtable] = Hash, [BaseVtable] = Hash };

    private static EmbeddedObject Embedded(int offset, string type) => new(Block + (ulong)offset, type, 0);

    private static MemoryHistoryStore Build((ulong Sequence, ulong Address, byte[] Bytes)[] writes)
        => MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Block, 0x100)], writes);
}
