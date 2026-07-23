using InternalsViewer.Query.Parsing;

namespace InternalsViewer.Query.Tests;

public class StatementParserTests
{
    private readonly StatementParser _parser = new();

    [Fact]
    public void Single_Select_Returns_Select()
    {
        var result = _parser.GetStatementType("SELECT * FROM dbo.Customer");

        Assert.Equal(StatementType.Select, result);
    }

    [Theory]
    [InlineData("INSERT INTO dbo.Customer (Name) VALUES ('A')")]
    [InlineData("UPDATE dbo.Customer SET Name = 'A' WHERE Id = 1")]
    [InlineData("DELETE FROM dbo.Customer WHERE Id = 1")]
    public void Single_Modification_Returns_Modification(string sql)
    {
        var result = _parser.GetStatementType(sql);

        Assert.Equal(StatementType.Modification, result);
    }

    [Fact]
    public void Merge_Statement_Returns_Modification()
    {
        var sql = """
                  MERGE dbo.Target AS t
                  USING dbo.Source AS s ON t.Id = s.Id
                  WHEN MATCHED THEN UPDATE SET t.Name = s.Name;
                  """;

        var result = _parser.GetStatementType(sql);

        Assert.Equal(StatementType.Modification, result);
    }

    [Fact]
    public void Select_Into_Returns_Modification()
    {
        var result = _parser.GetStatementType("SELECT * INTO dbo.Copy FROM dbo.Customer");

        Assert.Equal(StatementType.Modification, result);
    }

    [Fact]
    public void Multiple_Selects_Returns_MultiStatementSelect()
    {
        var sql = "SELECT 1; SELECT 2;";

        var result = _parser.GetStatementType(sql);

        Assert.Equal(StatementType.MultiStatementSelect, result);
    }

    [Fact]
    public void Multiple_Modifications_Returns_MultiStatementModification()
    {
        var sql = """
                  INSERT INTO dbo.Customer (Name) VALUES ('A');
                  UPDATE dbo.Customer SET Name = 'B' WHERE Id = 1;
                  """;

        var result = _parser.GetStatementType(sql);

        Assert.Equal(StatementType.MultiStatementModification, result);
    }

    [Fact]
    public void Exec_Statement_Returns_StoredProcedure()
    {
        var result = _parser.GetStatementType("EXEC dbo.GetCustomers");

        Assert.Equal(StatementType.StoredProcedure, result);
    }

    [Fact]
    public void Exec_Takes_Precedence_In_Mixed_Batch()
    {
        var sql = """
                  SELECT * FROM dbo.Customer;
                  EXEC dbo.GetCustomers;
                  """;

        var result = _parser.GetStatementType(sql);

        Assert.Equal(StatementType.StoredProcedure, result);
    }

    [Fact]
    public void Invalid_Sql_Returns_Invalid()
    {
        var result = _parser.GetStatementType("SELECT FROM WHERE");

        Assert.Equal(StatementType.Invalid, result);
    }

    [Fact]
    public void Mixed_Select_And_Modification_Returns_MultiStatementModification()
    {
        var sql = """
                  SELECT * FROM dbo.Customer;
                  DELETE FROM dbo.Customer WHERE Id = 1;
                  """;

        var result = _parser.GetStatementType(sql);

        Assert.Equal(StatementType.MultiStatementModification, result);
    }

    [Fact]
    public void Non_Select_Non_Modification_Single_Statement_Returns_Invalid()
    {
        var result = _parser.GetStatementType("CREATE TABLE dbo.T (Id INT)");

        Assert.Equal(StatementType.Invalid, result);
    }
}
