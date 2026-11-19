using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStoreHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts;

[Trait("Category", "Unit")]
public class DeclaredTypeMapTests
{
    private const ulong Program = 0x1F4_0000_1000;

    private const ulong Instructions = 0x1F4_0000_2000;

    [Fact]
    public void A_Block_A_Known_Object_Points_At_Takes_The_Type_The_Field_Declares()
    {
        var store = BuildStore(Instructions);

        var types = DeclaredTypeMap.Build(store, b => b == GetBlock(store, Program) ? "CEsComp" : null, _ => null);

        Assert.Equal((null, "CXCodeInst"), (types.Get(GetBlock(store, Program)), types.Get(GetBlock(store, Instructions))));
    }

    [Fact]
    public void A_Declared_Block_Knows_The_Block_That_Declared_It()
    {
        var store = BuildStore(Instructions);

        var types = DeclaredTypeMap.Build(store, b => b == GetBlock(store, Program) ? "CEsComp" : null, _ => null);

        Assert.Equal(GetBlock(store, Program), types.GetSource(GetBlock(store, Instructions)));
    }

    [Fact]
    public void A_Block_With_A_Class_Of_Its_Own_Keeps_It()
    {
        var store = BuildStore(Instructions);

        var types = DeclaredTypeMap.Build(store, b => b == GetBlock(store, Program) ? "CEsComp" : "CValComp", _ => null);

        Assert.Null(types.Get(GetBlock(store, Instructions)));
    }

    [Fact]
    public void A_Block_Whose_Content_Names_A_Class_Keeps_It()
    {
        var store = BuildStore(Instructions);

        var types = DeclaredTypeMap.Build(store, b => b == GetBlock(store, Program) ? "CEsComp" : null, _ => "CValComp");

        Assert.Null(types.Get(GetBlock(store, Instructions)));
    }

    [Fact]
    public void A_Pointer_Into_The_Middle_Of_A_Block_Declares_Nothing()
    {
        var store = BuildStore(Instructions + 0x20);

        var types = DeclaredTypeMap.Build(store, b => b == GetBlock(store, Program) ? "CEsComp" : null, _ => null);

        Assert.Null(types.Get(GetBlock(store, Instructions)));
    }

    private static MemoryHistoryStore BuildStore(ulong pointer)
        => Build([Block(Program, 0x80), Block(Instructions, 0x60)],
                 [(2, Program + 0x18, BitConverter.GetBytes(pointer)), (2, Instructions, [1])])
           .WithLayout(EngineLayout.Latest);
}
