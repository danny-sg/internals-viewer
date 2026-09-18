using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Symbols;

namespace InternalsViewer.Query.Tests;

public class SymbolSearchSuggestionTests
{
    [Fact]
    public void Operators_Become_Labelled_Search_Terms_In_The_Search_Syntax()
    {
        var suggestions = SymbolSearchSuggestion.FromOperators(CategoryMappings.Default);

        Assert.Contains(new SymbolSearchSuggestion("Hash Match Build", "sqlmin!CQScanHash::ConsumeBuild"), suggestions);
        Assert.Contains(new SymbolSearchSuggestion("Sort", "sqlmin!CBpQScanSort*|sqlmin!CQScanSort*|sqlmin!CQScanTopSort*"), suggestions);
        Assert.Contains(new SymbolSearchSuggestion("Hash Match", "sqlmin!CBpQScanHashJoin*|sqlmin!CQScanHash*"), suggestions);
        Assert.Equal(suggestions.OrderBy(s => s.Label, StringComparer.OrdinalIgnoreCase).Select(s => s.Label),
                     suggestions.Select(s => s.Label));
        Assert.Equal(suggestions.Count, suggestions.Select(s => s.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Categories_Become_Prefixed_Search_Terms_That_Or_Every_Rule_Of_The_Category()
    {
        var suggestions = SymbolSearchSuggestion.FromCategories(CategoryMappings.Default);

        var statements = Assert.Single(suggestions, s => s.Label == "Category: Statement Execution");

        Assert.Contains("sqllang!CXStmtSelect::XretExecute", statements.Term.Split(SymbolSearchSuggestion.Alternative));
        Assert.Contains("sqllang!CXStmtMerge::XretExecute", statements.Term.Split(SymbolSearchSuggestion.Alternative));
        Assert.Contains(suggestions, s => s.Label == "Category: Query Operator");
        Assert.Contains(suggestions, s => s.Label == "Category: SQL OS");
        Assert.DoesNotContain(suggestions, s => s.Label == "Category: Unknown");
        Assert.All(suggestions, s => Assert.StartsWith(SymbolSearchSuggestion.CategoryPrefix, s.Label));
    }

    [Fact]
    public void Categories_Are_Offered_Before_Operators()
    {
        var suggestions = SymbolSearchSuggestion.FromMappings(CategoryMappings.Default).ToList();

        var lastCategory = suggestions.FindLastIndex(s => s.Label.StartsWith(SymbolSearchSuggestion.CategoryPrefix));

        var firstOperator = suggestions.FindIndex(s => !s.Label.StartsWith(SymbolSearchSuggestion.CategoryPrefix));

        Assert.True(lastCategory >= 0 && firstOperator > lastCategory);
        Assert.Contains(new SymbolSearchSuggestion("Sort", "sqlmin!CBpQScanSort*|sqlmin!CQScanSort*|sqlmin!CQScanTopSort*"), suggestions);
    }

    [Fact]
    public void Matching_Is_On_Label_Or_Term_And_Blank_Text_Matches_Everything()
    {
        var suggestion = new SymbolSearchSuggestion("Hash Match (Aggregate)", "sqlmin!CQScanHashAgg*");

        Assert.True(suggestion.Matches("hash"));
        Assert.True(suggestion.Matches("aggregate"));
        Assert.True(suggestion.Matches("cqscanhashagg"));
        Assert.True(suggestion.Matches("  "));
        Assert.False(suggestion.Matches("sort"));
        Assert.Equal("Hash Match (Aggregate)", suggestion.Display);
    }
}
