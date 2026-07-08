using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Callstack.Categories;

namespace InternalsViewer.Query.Tests;

public class ResolvedCallstackFrameParserTests
{
    [Fact]
    public void Simple_Class_And_Method_With_Offset_Parses_Correctly()
    {
        var result = ResolvedCallstackFrameParser.Parse("sqlmin", "CQScan::GetRow+0x1a");

        Assert.Equal("sqlmin", result.Module);
        Assert.Equal("CQScan", result.ClassName);
        Assert.Equal("GetRow", result.MethodName);
        Assert.Equal(0x1au, result.Offset);
        Assert.Equal("CQScan::GetRow+0x1a", result.RawSymbol);
        Assert.Equal(ModuleCategory.StorageEngine, result.ModuleCategory);
        Assert.Equal(SymbolCategory.QueryOperator, result.SymbolCategory);
    }

    [Fact]
    public void Symbol_Without_Offset_Leaves_Offset_Null()
    {
        var result = ResolvedCallstackFrameParser.Parse("sqlmin", "CQScan::GetRow");

        Assert.Equal("CQScan", result.ClassName);
        Assert.Equal("GetRow", result.MethodName);
        Assert.Null(result.Offset);
    }

    [Fact]
    public void Symbol_Without_ClassName_Leaves_ClassName_Null()
    {
        var result = ResolvedCallstackFrameParser.Parse("kernel32", "RtlUserThreadStart+0x14");

        Assert.Null(result.ClassName);
        Assert.Equal("RtlUserThreadStart", result.MethodName);
        Assert.Equal(0x14u, result.Offset);
        Assert.Equal(SymbolCategory.System, result.SymbolCategory);
    }

    [Fact]
    public void Nested_Namespace_Splits_On_Final_Separator()
    {
        var result = ResolvedCallstackFrameParser.Parse("sqllang", "Outer::Inner::Method+0x5");

        Assert.Equal("Outer::Inner", result.ClassName);
        Assert.Equal("Method", result.MethodName);
        Assert.Equal(0x5u, result.Offset);
    }

    [Fact]
    public void ClassName_Strips_Backticks_And_Quotes()
    {
        var result = ResolvedCallstackFrameParser.Parse("sqlmin", "`anonymous namespace'::Helper+0x10");

        Assert.Equal("anonymous namespace", result.ClassName);
        Assert.Equal("Helper", result.MethodName);
        Assert.Equal(0x10u, result.Offset);
    }

    [Fact]
    public void Offset_Without_0x_Prefix_Still_Parses_As_Hex()
    {
        var result = ResolvedCallstackFrameParser.Parse("sqlmin", "Foo::Bar+1a2b");

        Assert.Equal(0x1a2bu, result.Offset);
    }

    [Theory]
    [InlineData("Foo::Bar+")]
    [InlineData("Foo::Bar+abcxyz")]
    public void Invalid_Or_Empty_Offset_Leaves_Offset_Null(string value)
    {
        var result = ResolvedCallstackFrameParser.Parse("sqlmin", value);

        Assert.Null(result.Offset);
    }

    [Fact]
    public void Unknown_Module_Returns_Unknown_Category()
    {
        var result = ResolvedCallstackFrameParser.Parse("someothermodule", "Foo::Bar");

        Assert.Equal(ModuleCategory.Unknown, result.ModuleCategory);
    }

    [Fact]
    public void Lambda_With_Nested_Namespace_In_Template_Args_Splits_On_TopLevel_Separator()
    {
        const string value =
            "CQDSManager::AcquireGenericQdsDbAndProcess<`CQDSManager::FGetStatementContextIdAndEpoch'::`2'::<lambda_1> >+0x99";

        var result = ResolvedCallstackFrameParser.Parse("sqllang", value);

        Assert.Equal("CQDSManager", result.ClassName);
        Assert.Equal(
            "AcquireGenericQdsDbAndProcess<`CQDSManager::FGetStatementContextIdAndEpoch'::`2'::<lambda_1> >",
            result.MethodName);
        Assert.Equal(0x99u, result.Offset);
        Assert.Equal(value, result.RawSymbol);
        Assert.Equal(SymbolCategory.QueryStore, result.SymbolCategory);
    }
}
