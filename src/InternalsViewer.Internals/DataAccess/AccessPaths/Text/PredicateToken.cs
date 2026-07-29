namespace InternalsViewer.Internals.DataAccess.AccessPaths.Text;

/// <summary>
/// A single run of predicate text together with the role it plays
/// </summary>
/// <remarks>
/// A token carries an optional description so a renderer can offer detail the text alone does not show, such as the plan parameter a
/// literal was resolved from.
/// </remarks>
public readonly record struct PredicateToken(PredicateTokenType Type,
                                             string Text,
                                             string? Description = null)
{
    public override string ToString()
    {
        return Text;
    }
}
