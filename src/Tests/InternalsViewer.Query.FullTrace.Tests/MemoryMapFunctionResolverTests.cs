using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryMapFunctionResolverTests
{
    private const uint Rva = 0x1000;

    [Fact]
    public void A_Member_Function_Is_Visited_With_Its_Declared_Parameters()
    {
        var collector = new MemoryMapFunctionResolver.Collector();

        collector.Visit(Symbol("CQueryScan::StartupQuery(unsigned __int64 *,BaseXact *,HkTransaction *)",
                               "?StartupQuery@CQueryScan@@QEAAHPEA_KPEAVBaseXact@@PEAVHkTransaction@@@Z",
                               isFirst: true));

        var function = Assert.Single(collector.Build());

        Assert.Equal(MemoryMapOperation.Visited, function.Operation);
        Assert.Equal([(2, "BaseXact"), (3, "HkTransaction")], function.Declared.Select(d => (d.Slot, d.Name)));
    }

    [Fact]
    public void A_Constructor_Folded_With_Another_Function_Is_Only_Visited()
    {
        var collector = new MemoryMapFunctionResolver.Collector();

        collector.Visit(Symbol("CQScanNew::CQScanNew(void)", "??0CQScanNew@@QEAA@XZ", isFirst: true));

        collector.Visit(Symbol("CQScanRangeNew::CQScanRangeNew(void)", "??0CQScanRangeNew@@QEAA@XZ", isFirst: false, hasSameName: false));

        Assert.Equal(MemoryMapOperation.Visited, Assert.Single(collector.Build()).Operation);
    }

    [Fact]
    public void Folded_Functions_That_Disagree_On_Parameters_Declare_Nothing()
    {
        var collector = new MemoryMapFunctionResolver.Collector();

        collector.Visit(Symbol("Startup(BaseXact *)", "?Startup@@YAXPEAVBaseXact@@@Z", isFirst: true));

        collector.Visit(Symbol("Startup(HkTransaction *)", "?Startup@@YAXPEAVHkTransaction@@@Z", isFirst: false, hasSameParameters: false));

        Assert.Empty(collector.Build());
    }

    [Fact]
    public void A_Free_Function_Without_Class_Pointers_Is_Not_Hooked()
    {
        var collector = new MemoryMapFunctionResolver.Collector();

        collector.Visit(Symbol("Startup(int)", "?Startup@@YAXH@Z", isFirst: true));

        Assert.Empty(collector.Build());
    }

    [Fact]
    public void Class_Pointers_In_Registers_And_The_Return_Are_Declared()
    {
        var signature = new FunctionSignature(["XDES *",
                                               "HoBtAccess *",
                                               "unsigned long",
                                               "enum AccessorFlagEx",
                                               "unsigned __int64",
                                               "tagDBBINDING const * const"],
                                              FunctionType.Member,
                                              "AccBindings *");

        Assert.Equal([(1, "XDES"), (2, "HoBtAccess"), (ArgumentLayout.ReturnRegister, "AccBindings")],
                     MemoryMapFunctionResolver.GetDeclared(signature).Select(d => (d.Slot, d.Name)));
    }

    [Fact]
    public void A_Free_Function_Declares_From_Its_First_Register()
    {
        var signature = new FunctionSignature(["CMsqlReadOnlyXact *", "void *", "char const *", "unsigned __int64 *"],
                                              FunctionType.Free,
                                              "bool");

        Assert.Equal([(0, "CMsqlReadOnlyXact")], MemoryMapFunctionResolver.GetDeclared(signature).Select(d => (d.Slot, d.Name)));
    }

    [Fact]
    public void A_Class_Returned_By_Value_Is_Not_Declared()
    {
        var signature = new FunctionSignature([], FunctionType.Member, "CTypeInfo", HasReturnBuffer: true);

        Assert.Empty(MemoryMapFunctionResolver.GetDeclared(signature));
    }

    private static FunctionSymbol Symbol(string signature,
                                        string decoratedName,
                                        bool isFirst,
                                        bool hasSameName = true,
                                        bool hasSameParameters = true)
        => new()
        {
            Rva = Rva,
            Name = signature[..signature.IndexOf('(')],
            Signature = signature,
            DecoratedName = decoratedName,
            IsFirst = isFirst,
            HasSameName = hasSameName,
            HasSameParameters = hasSameParameters
        };
}
