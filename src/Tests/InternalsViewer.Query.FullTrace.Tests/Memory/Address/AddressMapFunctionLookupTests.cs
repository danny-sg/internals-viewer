using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.Events.Parsing;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Address;

[Trait("Category", "Unit")]
public class AddressMapFunctionLookupTests
{
    private const uint Rva = 0x1000;

    [Fact]
    public void A_Member_Function_Is_Visited_With_Its_Declared_Parameters()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions,
              ("CQueryScan::StartupQuery(unsigned __int64 *,BaseXact *,HkTransaction *)",
               "?StartupQuery@CQueryScan@@QEAAHPEA_KPEAVBaseXact@@PEAVHkTransaction@@@Z"));

        var function = Assert.Single(functions);

        Assert.Equal(AddressMapOperation.Visited, function.Operation);
        Assert.Equal([(2, "BaseXact"), (3, "HkTransaction")], function.DeclaredClasses.Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void A_Constructor_Folded_With_Another_Function_Is_Only_Visited()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions,
              ("CQScanNew::CQScanNew(void)", "??0CQScanNew@@QEAA@XZ"),
              ("CQScanRangeNew::CQScanRangeNew(void)", "??0CQScanRangeNew@@QEAA@XZ"));

        Assert.Equal(AddressMapOperation.Visited, Assert.Single(functions).Operation);
    }

    [Fact]
    public void A_Member_Function_Folded_With_Another_Class_Names_Neither_Its_Object_Nor_Its_Return()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions,
              ("ExceptionBackout::GetCurrentException(void)", "?GetCurrentException@ExceptionBackout@@QEBAPEBVSQLError@@XZ"),
              ("CPageInfo::PmoGetOwner(void)", "?PmoGetOwner@CPageInfo@@QEBAPEAVIMemObj@@XZ"));

        Assert.Empty(functions);
    }

    [Fact]
    public void A_Member_Function_Folded_With_Another_Class_Still_Declares_Its_Parameters()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions,
              ("CQueryScan::StartupQuery(BaseXact *)", "?StartupQuery@CQueryScan@@QEAAHPEAVBaseXact@@@Z"),
              ("CQueryExecContext::Init(BaseXact *)", "?Init@CQueryExecContext@@QEAAHPEAVBaseXact@@@Z"));

        var function = Assert.Single(functions);

        Assert.Equal(AddressMapOperation.Declared, function.Operation);
        Assert.Equal([(1, "BaseXact")], function.DeclaredClasses.Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void Member_Functions_Of_One_Class_Folded_Together_Still_Name_Its_Object()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions,
              ("CPageInfo::PmoGetOwner(void)", "?PmoGetOwner@CPageInfo@@QEBAPEAVIMemObj@@XZ"),
              ("CPageInfo::GetOwner(void)", "?GetOwner@CPageInfo@@QEBAPEAVIMemObj@@XZ"));

        Assert.Equal(AddressMapOperation.Visited, Assert.Single(functions).Operation);
    }

    [Fact]
    public void Untyped_Pointers_In_Registers_And_On_The_Stack_Are_Listed_Apart_From_Class_Pointers()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions,
              ("IndexDataSetSession::GetNextRowValuesInternal(enum RowScanDirection,DataAccessWrapper *,"
               + "enum StartScanMode,unsigned __int64 *)",
               "?GetNextRowValuesInternal@IndexDataSetSession@@QEAAHW4RowScanDirection@@PEAVDataAccessWrapper@@"
               + "W4StartScanMode@@PEA_K@Z"));

        var function = Assert.Single(functions);

        Assert.Equal([(2, "DataAccessWrapper")], function.DeclaredClasses.Select(d => (Slot: d.ArgumentSlot, d.Name)));
        Assert.Equal([4], function.Pointers);
    }

    [Fact]
    public void A_Free_Function_Taking_Only_Untyped_Pointers_Is_Kept_For_Them()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions, ("Fetch(unsigned __int64 *,int)", "?Fetch@@YAXPEA_KH@Z"));

        var function = Assert.Single(functions);

        Assert.Equal((AddressMapOperation.Declared, 0), (function.Operation, function.DeclaredClasses.Length));
        Assert.Equal([0], function.Pointers);
    }

    [Fact]
    public void Folded_Functions_That_Disagree_On_Parameters_Declare_Nothing()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions,
              ("Startup(BaseXact *)", "?Startup@@YAXPEAVBaseXact@@@Z"),
              ("Startup(HkTransaction *)", "?Startup@@YAXPEAVHkTransaction@@@Z"));

        Assert.Empty(functions);
    }

    [Fact]
    public void A_Free_Function_Without_Class_Pointers_Is_Not_Hooked()
    {
        var functions = new List<AddressMapFunction>();

        Visit(functions, ("Startup(int)", "?Startup@@YAXH@Z"));

        Assert.Empty(functions);
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
                     AddressMapFunctionLookup.GetDeclared(ArgumentLayout.GetLayout(signature)).Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void A_Free_Function_Declares_From_Its_First_Register()
    {
        var signature = new FunctionSignature(["CMsqlReadOnlyXact *", "void *", "char const *", "unsigned __int64 *"],
                                              FunctionType.Free,
                                              "bool");

        Assert.Equal([(0, "CMsqlReadOnlyXact")], AddressMapFunctionLookup.GetDeclared(ArgumentLayout.GetLayout(signature)).Select(d => (Slot: d.ArgumentSlot, d.Name)));
    }

    [Fact]
    public void A_Class_Returned_By_Value_Is_Not_Declared()
    {
        var signature = new FunctionSignature([], FunctionType.Member, "CTypeInfo", HasReturnBuffer: true);

        Assert.Empty(AddressMapFunctionLookup.GetDeclared(ArgumentLayout.GetLayout(signature)));
    }

    private static void Visit(List<AddressMapFunction> functions, params (string Signature, string DecoratedName)[] symbols)
    {
        var builder = new SymbolTable.Builder();

        foreach (var (signature, decoratedName) in symbols)
        {
            builder.Add(Rva, true, signature[..signature.IndexOf('(')], signature, decoratedName);
        }

        var reader = builder.Build().Read();

        var classes = new StringInternPool();

        while (reader.MoveNext())
        {
            var layout = reader.Parsed is { } parsed ? ArgumentLayout.GetLayout(parsed) : null;

            if (reader.IsFirst && AddressMapFunctionLookup.GetFunction(reader, layout, classes) is { } function)
            {
                functions.Add(function);
            }
        }
    }
}
