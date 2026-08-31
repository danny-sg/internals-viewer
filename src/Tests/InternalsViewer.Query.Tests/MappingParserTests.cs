using InternalsViewer.Query.CallStack.Categories;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class MappingParserTests
{
    [Fact]
    public void Skips_Comments_Blank_Lines_And_Header()
    {
        var content = "# a comment\n\nModule|Class|Function|Category\nsqlmin|CQScanTop*|*|QueryOperator\n";

        var rules = MappingParser.ParseSymbols(new StringReader(content)).ToList();

        Assert.Equal(SymbolCategory.QueryOperator, Assert.Single(rules).Category);
    }

    [Fact]
    public void Symbol_Row_Ignores_Anything_Past_The_Category()
    {
        // Trailing cells are a leftover from when Iterator and PlanOperator lived here; a row that still carries them
        // must not be dropped for it.
        var rules = MappingParser.ParseSymbols(new StringReader("*|*|GetRow*|RowAccess|Row Iterator|")).ToList();

        Assert.Equal(SymbolCategory.RowAccess, Assert.Single(rules).Category);
    }

    [Fact]
    public void Plan_Operator_Cell_Takes_A_List()
    {
        // One class serves plan operators with unrelated names — CQScanRange is the seek and the Key Lookup — so a
        // single pattern cannot cover it and two rules would just override each other.
        var rules = MappingParser.ParseOperators(
            new StringReader("sqlmin|CQScanRange*|*|Index Seek| *Index S* , Key Lookup ")).ToList();

        var planOperator = Assert.Single(rules).PlanOperator;

        Assert.True(planOperator.Any(p => p.Matches("Clustered Index Seek")));
        Assert.True(planOperator.Any(p => p.Matches("Key Lookup")));
        Assert.False(planOperator.Any(p => p.Matches("Table Scan")));
    }

    [Fact]
    public void An_Operator_Row_Stating_Only_A_Badge_Still_Parses()
    {
        // minCells is 4, so a rule that names a badge and no boundary must survive rather than be dropped in silence.
        var rules = MappingParser.ParseOperators(new StringReader("sqlmin|CQScanProfileNew|GetRow|Profiling")).ToList();

        var rule = Assert.Single(rules);

        Assert.Equal("Profiling", rule.Iterator);
        Assert.Empty(rule.PlanOperator);
    }

    [Fact]
    public void Empty_Plan_Operator_Cell_Is_Empty_Not_A_Match_Anything_Pattern()
    {
        // GlobPattern.Parse("") matches everything, which here would make every unmapped frame an operator boundary.
        var rules = MappingParser.ParseOperators(new StringReader("sqlmin|Rowset*|*FetchNextRow|Row Iterator|")).ToList();

        Assert.Empty(Assert.Single(rules).PlanOperator);
    }

    [Fact]
    public void Operator_Header_Row_Is_Skipped()
    {
        // No cell here fails to parse, unlike the category files, so the header has to be dropped by name.
        var content = string.Join('\n', "Module|Class|Function|Iterator|PlanOperator", "sqlmin|CQScanTop*|*|Top|Top");

        Assert.Equal("Top", Assert.Single(MappingParser.ParseOperators(new StringReader(content))).Iterator);
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
