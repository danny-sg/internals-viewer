using InternalsViewer.Query.Parsing;

namespace InternalsViewer.Query.Tests;

public class QueryParserTests
{
    [Fact]
    public void SplitCommands_Splits_On_GO_Batch_Separator()
    {
        var sql = "SELECT 1\nGO\nSELECT 2\nGO\nSELECT 3";

        var result = QueryParser.SplitCommands(sql);

        Assert.Equal(["SELECT 1\n", "\nSELECT 2\n", "\nSELECT 3"], result);
    }

    [Fact]
    public void SplitCommands_Is_Case_Insensitive_And_Allows_Surrounding_Whitespace()
    {
        var sql = "SELECT 1\n  go  \nSELECT 2";

        var result = QueryParser.SplitCommands(sql);

        Assert.Equal(["SELECT 1\n", "\nSELECT 2"], result);
    }

    [Fact]
    public void SplitCommands_With_No_GO_Returns_Single_Command()
    {
        var sql = "SELECT 1; SELECT 2;";

        var result = QueryParser.SplitCommands(sql);

        Assert.Equal([sql], result);
    }

    [Fact]
    public void SplitCommands_Drops_Blank_Entries()
    {
        var sql = "GO\nGO\nSELECT 1";

        var result = QueryParser.SplitCommands(sql);

        Assert.Equal(["\nSELECT 1"], result);
    }

    [Fact]
    public void Parse_With_No_TrackedSelection_Returns_All_Commands_And_No_Pre_Or_Post()
    {
        var payload = new ExecuteSqlPayload("SELECT 1\nGO\nSELECT 2",
                                            new QueryOptions(),
                                            StatementType.MultiStatementSelect,
                                            null);

        var (preCommands, commands, postCommands) = QueryParser.Parse(payload);

        Assert.Empty(preCommands);
        Assert.Empty(postCommands);
        Assert.Equal(2, commands.Length);
    }

    [Fact]
    public void Parse_With_TrackedSelection_Splits_Into_Pre_Tracked_And_Post()
    {
        var sql = "SELECT 'pre'\nGO\nSELECT 'tracked'\nGO\nSELECT 'post'";

        var start = sql.IndexOf("SELECT 'tracked'", StringComparison.Ordinal);
        var end = start + "SELECT 'tracked'".Length;

        var payload = new ExecuteSqlPayload(sql,
                                            new QueryOptions(),
                                            StatementType.Select,
                                            new TrackedSelectionRange(start, end));

        var (preCommands, commands, postCommands) = QueryParser.Parse(payload);

        Assert.Single(preCommands);
        Assert.Contains("pre", preCommands[0]);

        Assert.Single(commands);
        Assert.Contains("tracked", commands[0]);

        Assert.Single(postCommands);
        Assert.Contains("post", postCommands[0]);
    }

    [Fact]
    public void Parse_Clamps_Negative_Start_To_Zero()
    {
        var payload = new ExecuteSqlPayload("SELECT 1",
                                            new QueryOptions(),
                                            StatementType.Select,
                                            new TrackedSelectionRange(-5, 8));

        var (preCommands, commands, postCommands) = QueryParser.Parse(payload);

        Assert.Empty(preCommands);
        Assert.Empty(postCommands);
        Assert.Single(commands);
        Assert.Equal("SELECT 1", commands[0]);
    }

    [Fact]
    public void Parse_Clamps_End_Beyond_SqlText_Length()
    {
        var payload = new ExecuteSqlPayload("SELECT 1",
                                            new QueryOptions(),
                                            StatementType.Select,
                                            new TrackedSelectionRange(0, 1000));

        var (_, commands, postCommands) = QueryParser.Parse(payload);

        Assert.Empty(postCommands);
        Assert.Single(commands);
        Assert.Equal("SELECT 1", commands[0]);
    }

    [Fact]
    public void Parse_Clamps_End_Before_Start_To_Start()
    {
        var payload = new ExecuteSqlPayload("SELECT 1",
                                            new QueryOptions(),
                                            StatementType.Select,
                                            new TrackedSelectionRange(5, 1));

        var (preCommands, commands, postCommands) = QueryParser.Parse(payload);

        Assert.Single(preCommands);
        Assert.Equal("SELEC", preCommands[0]);

        Assert.Empty(commands);

        Assert.Single(postCommands);
        Assert.Equal("T 1", postCommands[0]);
    }
}
