using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace InternalsViewer.Query.Parsing;

public class StatementParser
{
    private TSqlParser Parser { get; } = new TSql150Parser(false);

    public StatementType GetStatementType(string sqlText)
    {
        IList<ParseError> errors;

        TSqlFragment fragment;

        using (var reader = new StringReader(sqlText))
        {
            fragment = Parser.Parse(reader, out errors);
        }

        if (errors is { Count: > 0 })
        {
            return StatementType.Invalid;
        }

        var batch = (TSqlScript)fragment;

        var statements = batch.Batches
                              .SelectMany(b => b.Statements)
                              .ToList();

        var isMultiStatement = statements.Count > 1;

        var isModification = statements.Any(s =>
                                            s is InsertStatement ||
                                            s is UpdateStatement ||
                                            s is DeleteStatement ||
                                            s is MergeStatement ||
                                            IsSelectInto(s)
        );

        var isExec = statements.Any(s => s is ExecuteStatement);

        var isSelect = statements.All(s => s is SelectStatement);

        if (isExec)
        {
            return StatementType.StoredProcedure;
        }

        if (isMultiStatement && isModification)
        {
            return StatementType.MultiStatementModification;
        }
       
        if(isMultiStatement && isSelect)
        {
            return StatementType.MultiStatementSelect;
        }

        if(isModification)
        {
            return StatementType.Modification;
        }

        if(isSelect)
        {
            return StatementType.Select;
        }

        return StatementType.Invalid;
    }

    private static bool IsSelectInto(TSqlStatement stmt)
    {
        if (stmt is SelectStatement select)
        {
            return select.Into != null;
        }

        return false;
    }
}
