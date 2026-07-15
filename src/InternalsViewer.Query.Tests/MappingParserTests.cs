using InternalsViewer.Query.CallStack.Categories;

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
    public void Plan_Operator_Cell_Is_Parsed_As_A_Pattern()
    {
        var rules = MappingParser.ParseSymbols(new StringReader("sqlmin|CQScanRange*|*|QueryOperator|Index Seek|*Index Seek"))
                                 .ToList();

        var planOperator = Assert.Single(rules).PlanOperator;

        Assert.True(planOperator.Any(p => p.Matches("Clustered Index Seek")));
    }

    [Fact]
    public void Plan_Operator_Cell_Takes_A_List()
    {
        // One class serves plan operators with unrelated names — CQScanRange is the seek and the Key Lookup — so a
        // single pattern cannot cover it and two rules would just override each other.
        var rules = MappingParser.ParseSymbols(
            new StringReader("sqlmin|CQScanRange*|*|QueryOperator|Index Seek| *Index S* , Key Lookup ")).ToList();

        var planOperator = Assert.Single(rules).PlanOperator;

        Assert.True(planOperator.Any(p => p.Matches("Clustered Index Seek")));
        Assert.True(planOperator.Any(p => p.Matches("Key Lookup")));
        Assert.False(planOperator.Any(p => p.Matches("Table Scan")));
    }

    [Fact]
    public void Row_Without_A_Plan_Operator_Cell_Still_Parses()
    {
        // The column is a late addition and minCells stays at 5, so a five-cell rule must survive rather than be
        // dropped in silence — it simply marks no operator boundary.
        var rules = MappingParser.ParseSymbols(new StringReader("*|*|GetRow*|RowAccess|")).ToList();

        var rule = Assert.Single(rules);

        Assert.Equal(SymbolCategory.RowAccess, rule.Category);
        Assert.Empty(rule.PlanOperator);
    }

    [Fact]
    public void Empty_Plan_Operator_Cell_Is_Empty_Not_A_Match_Anything_Pattern()
    {
        // GlobPattern.Parse("") matches everything, which here would make every unmapped frame an operator boundary.
        var rules = MappingParser.ParseSymbols(new StringReader("*|*|GetRow*|RowAccess|Row Iterator|")).ToList();

        Assert.Empty(Assert.Single(rules).PlanOperator);
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
