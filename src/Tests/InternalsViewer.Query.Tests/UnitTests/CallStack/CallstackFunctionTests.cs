using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;

namespace InternalsViewer.Query.Tests.UnitTests.CallStack;

[Trait("Category", "Unit")]
public class CallstackFunctionTests
{
    [Fact]
    public void Simple_Class_And_Method_With_Offset_Parses_Correctly()
    {
        var frame = Resolve("sqlmin", "CQScan::GetRow+0x1a");

        Assert.Equal("CQScan", frame.Function!.ClassName);
        Assert.Equal("GetRow", frame.Function.MethodName);
        Assert.Equal("CQScan::GetRow", frame.Function.Name);
        Assert.Equal(0x1au, frame.Offset);
        Assert.Equal(ModuleCategory.StorageEngine, frame.Module.Category);
        Assert.Equal(SymbolCategory.QueryOperator, frame.Function.SymbolCategory);
    }

    [Fact]
    public void Operator_Frame_Populates_Iterator()
    {
        var function = CallstackFunction.Parse("sqlmin", "CQScanTopNew::GetRow");

        Assert.Equal(SymbolCategory.QueryOperator, function.SymbolCategory);
        Assert.Equal("Top", function.Iterator);
    }

    [Fact]
    public void Symbol_Without_Offset_Leaves_Offset_Null()
    {
        var frame = Resolve("sqlmin", "CQScan::GetRow");

        Assert.Equal("CQScan", frame.Function!.ClassName);
        Assert.Equal("GetRow", frame.Function.MethodName);
        Assert.Null(frame.Offset);
    }

    [Fact]
    public void Symbol_Without_ClassName_Leaves_ClassName_Null()
    {
        var frame = Resolve("kernel32", "RtlUserThreadStart+0x14");

        Assert.Null(frame.Function!.ClassName);
        Assert.Equal("RtlUserThreadStart", frame.Function.MethodName);
        Assert.Equal(0x14u, frame.Offset);
        Assert.Equal(SymbolCategory.System, frame.Function.SymbolCategory);
    }

    [Fact]
    public void Nested_Namespace_Splits_On_Final_Separator()
    {
        var function = CallstackFunction.Parse("sqllang", "Outer::Inner::Method");

        Assert.Equal("Outer::Inner", function.ClassName);
        Assert.Equal("Method", function.MethodName);
    }

    [Fact]
    public void ClassName_Strips_Backticks_And_Quotes()
    {
        var function = CallstackFunction.Parse("sqlmin", "`anonymous namespace'::Helper");

        Assert.Equal("anonymous namespace", function.ClassName);
        Assert.Equal("Helper", function.MethodName);
    }

    [Fact]
    public void Offset_Without_0x_Prefix_Still_Parses_As_Hex()
    {
        Assert.Equal(0x1a2bu, Resolve("sqlmin", "Foo::Bar+1a2b").Offset);
    }

    [Theory]
    [InlineData("Foo::Bar+")]
    [InlineData("Foo::Bar+abcxyz")]
    public void Invalid_Or_Empty_Offset_Leaves_Offset_Null(string value)
    {
        Assert.Null(Resolve("sqlmin", value).Offset);
    }

    [Fact]
    public void Unknown_Module_Returns_Unknown_Category()
    {
        Assert.Equal(ModuleCategory.Unknown, new CallstackModule("someothermodule").Category);
    }

    [Fact]
    public void Frames_Of_One_Function_Share_It()
    {
        var functions = new CallstackFunctionLookup();

        var first = new CallstackFrame { Module = new("sqlmin") };

        var second = new CallstackFrame { Module = new("sqlmin") };

        functions.Resolve(first, "CQScan::GetRow+0x1a");

        functions.Resolve(second, "CQScan::GetRow+0x40");

        Assert.Same(first.Function, second.Function);
        Assert.Equal((0x1au, 0x40u), (first.Offset, second.Offset));
    }

    [Fact]
    public void Lambda_With_Nested_Namespace_In_Template_Args_Splits_On_TopLevel_Separator()
    {
        const string name = "CQDSManager::AcquireGenericQdsDbAndProcess<`CQDSManager::FGetStatementContextIdAndEpoch'::`2'::<lambda_1> >";

        var frame = Resolve("sqllang", name + "+0x99");

        Assert.Equal("CQDSManager", frame.Function!.ClassName);
        Assert.Equal("AcquireGenericQdsDbAndProcess<`CQDSManager::FGetStatementContextIdAndEpoch'::`2'::<lambda_1> >",
                     frame.Function.MethodName);
        Assert.Equal(0x99u, frame.Offset);
        Assert.Equal(name, frame.Function.Name);
        Assert.Equal(SymbolCategory.QueryStore, frame.Function.SymbolCategory);
    }

    private static CallstackFrame Resolve(string module, string symbol)
    {
        var frame = new CallstackFrame { Module = new(module) };

        new CallstackFunctionLookup().Resolve(frame, symbol);

        return frame;
    }
}
