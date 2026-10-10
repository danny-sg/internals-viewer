namespace InternalsViewer.Query.Parsing.Sql;

public readonly record struct SqlToken(SqlTokenType Type, string Text)
{
    public override string ToString()
    {
        return Text;
    }
}
