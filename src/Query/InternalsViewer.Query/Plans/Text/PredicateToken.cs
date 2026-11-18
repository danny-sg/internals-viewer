namespace InternalsViewer.Execution.AccessPaths.Text;

/// <summary>
/// A single run of predicate text/token type
/// </summary>
public readonly record struct PredicateToken(PredicateTokenType Type,
                                             string Text,
                                             string? Description = null)
{
    public override string ToString()
    {
        return Text;
    }
}
