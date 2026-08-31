namespace InternalsViewer.Query.Parsing;

public readonly record struct SqlToken(SqlTokenType Type, string Text)
{
    public override string ToString()
    {
        return Text;
    }
}
