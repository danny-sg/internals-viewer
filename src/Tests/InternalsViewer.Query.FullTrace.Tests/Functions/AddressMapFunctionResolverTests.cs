using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Functions;

[Trait("Category", "Unit")]
public class AddressMapFunctionResolverTests
{
    private const uint Rva = 0x1000;

    [Fact]
    public void A_Member_Function_Is_Visited_With_Its_Declared_Parameters()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("CQueryScan::StartupQuery(unsigned __int64 *,BaseXact *,HkTransaction *)",
                               "?StartupQuery@CQueryScan@@QEAAHPEA_KPEAVBaseXact@@PEAVHkTransaction@@@Z",
                               isFirst: true));

        var function = Assert.Single(collector.Build());

        Assert.Equal(AddressMapOperation.Visited, function.Operation);
        Assert.Equal([(2, "BaseXact"), (3, "HkTransaction")], function.Declared.Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void A_Constructor_Folded_With_Another_Function_Is_Only_Visited()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("CQScanNew::CQScanNew(void)", "??0CQScanNew@@QEAA@XZ", isFirst: true));

        collector.Visit(Symbol("CQScanRangeNew::CQScanRangeNew(void)", "??0CQScanRangeNew@@QEAA@XZ", isFirst: false, hasSameName: false));

        Assert.Equal(AddressMapOperation.Visited, Assert.Single(collector.Build()).Operation);
    }

    [Fact]
    public void A_Member_Function_Folded_With_Another_Class_Names_Neither_Its_Object_Nor_Its_Return()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("ExceptionBackout::GetCurrentException(void)",
                               "?GetCurrentException@ExceptionBackout@@QEBAPEBVSQLError@@XZ",
                               isFirst: true));

        collector.Visit(Symbol("CPageInfo::PmoGetOwner(void)",
                               "?PmoGetOwner@CPageInfo@@QEBAPEAVIMemObj@@XZ",
                               isFirst: false,
                               hasSameName: false));

        Assert.Empty(collector.Build());
    }

    [Fact]
    public void A_Member_Function_Folded_With_Another_Class_Still_Declares_Its_Parameters()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("CQueryScan::StartupQuery(BaseXact *)", "?StartupQuery@CQueryScan@@QEAAHPEAVBaseXact@@@Z", isFirst: true));

        collector.Visit(Symbol("CQueryExecContext::Init(BaseXact *)",
                               "?Init@CQueryExecContext@@QEAAHPEAVBaseXact@@@Z",
                               isFirst: false,
                               hasSameName: false));

        var function = Assert.Single(collector.Build());

        Assert.Equal(AddressMapOperation.Declared, function.Operation);
        Assert.Equal([(1, "BaseXact")], function.Declared.Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void Member_Functions_Of_One_Class_Folded_Together_Still_Name_Its_Object()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("CPageInfo::PmoGetOwner(void)", "?PmoGetOwner@CPageInfo@@QEBAPEAVIMemObj@@XZ", isFirst: true));

        collector.Visit(Symbol("CPageInfo::GetOwner(void)",
                               "?GetOwner@CPageInfo@@QEBAPEAVIMemObj@@XZ",
                               isFirst: false,
                               hasSameName: false));

        Assert.Equal(AddressMapOperation.Visited, Assert.Single(collector.Build()).Operation);
    }

    [Fact]
    public void Untyped_Pointers_In_Registers_And_On_The_Stack_Are_Listed_Apart_From_Class_Pointers()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("IndexDataSetSession::GetNextRowValuesInternal(enum RowScanDirection,DataAccessWrapper *,"
                               + "enum StartScanMode,unsigned __int64 *)",
                               "?GetNextRowValuesInternal@IndexDataSetSession@@QEAAHW4RowScanDirection@@PEAVDataAccessWrapper@@"
                               + "W4StartScanMode@@PEA_K@Z",
                               isFirst: true));

        var function = Assert.Single(collector.Build());

        Assert.Equal([(2, "DataAccessWrapper")], function.Declared.Select(d => (Slot: d.ArgumentSlot, d.Name)));
        Assert.Equal([4], function.Pointers);
    }

    [Fact]
    public void A_Free_Function_Taking_Only_Untyped_Pointers_Is_Kept_For_Them()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("Fetch(unsigned __int64 *,int)", "?Fetch@@YAXPEA_KH@Z", isFirst: true));

        var function = Assert.Single(collector.Build());

        Assert.Equal((AddressMapOperation.Declared, 0), (function.Operation, function.Declared.Length));
        Assert.Equal([0], function.Pointers);
    }

    [Fact]
    public void Folded_Functions_That_Disagree_On_Parameters_Declare_Nothing()
    {
        var collector = new AddressMapFunctionResolver.Collector();

        collector.Visit(Symbol("Startup(BaseXact *)", "?Startup@@YAXPEAVBaseXact@@@Z", isFirst: true));

        collector.Visit(Symbol("Startup(HkTransaction *)", "?Startup@@YAXPEAVHkTransaction@@@Z", isFirst: false, hasSameParameters: false));

        Assert.Empty(collector.Build());
    }

    [Fact]
    public void A_Free_Function_Without_Class_Pointers_Is_Not_Hooked()
    {
        var collector = new AddressMapFunctionResolver.Collector();

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
                     AddressMapFunctionResolver.GetDeclared(signature).Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void A_Free_Function_Declares_From_Its_First_Register()
    {
        var signature = new FunctionSignature(["CMsqlReadOnlyXact *", "void *", "char const *", "unsigned __int64 *"],
                                              FunctionType.Free,
                                              "bool");

        Assert.Equal([(0, "CMsqlReadOnlyXact")], AddressMapFunctionResolver.GetDeclared(signature).Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void A_Class_Returned_By_Value_Is_Not_Declared()
    {
        var signature = new FunctionSignature([], FunctionType.Member, "CTypeInfo", HasReturnBuffer: true);

        Assert.Empty(AddressMapFunctionResolver.GetDeclared(signature));
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
