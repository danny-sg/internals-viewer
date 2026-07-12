using InternalsViewer.Query.Callstack.Categories;

namespace InternalsViewer.Query.Tests;

public class MappingParserTests
{
    [Fact]
    public void Skips_Comments_Blank_Lines_And_Header()
    {
        var content = "# a comment\n\nModule|Class|Function|Category|Iterator\nsqlmin|CQScanTop*|*|QueryOperator|Top\n";

        var rules = MappingParser.ParseSymbols(new StringReader(content)).ToList();

        var rule = Assert.Single(rules);
        Assert.Equal(SymbolCategory.QueryOperator, rule.Category);
        Assert.Equal("Top", rule.Iterator);
    }

    [Fact]
    public void Empty_Iterator_Cell_Is_Null()
    {
        var rules = MappingParser.ParseSymbols(new StringReader("*|*|GetRow*|RowAccess|")).ToList();

        Assert.Null(Assert.Single(rules).Iterator);
    }

    [Fact]
    public void Row_With_Unknown_Category_Is_Skipped()
    {
        var content = "*|*|Foo*|NotARealCategory|\n*|*|Bar*|Locking|";

        var rules = MappingParser.ParseSymbols(new StringReader(content)).ToList();

        Assert.Equal(SymbolCategory.Locking, Assert.Single(rules).Category);
    }

    [Fact]
    public void Modules_Parse_And_Skip_Header()
    {
        var modules = MappingParser.ParseModules(new StringReader("Module|ModuleCategory\nsqlmin|StorageEngine\n"))
                                   .ToList();

        Assert.Equal(("sqlmin", ModuleCategory.StorageEngine), Assert.Single(modules));
    }
}
