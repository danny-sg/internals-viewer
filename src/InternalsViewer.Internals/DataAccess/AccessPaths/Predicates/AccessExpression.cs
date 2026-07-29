using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;

/// <summary>
/// A scalar expression appearing in a predicate
/// </summary>
public abstract record AccessExpression
{
    /// <summary>
    /// A reference to a column of the row being examined
    /// </summary>
    public sealed record Column(int Ordinal, string Name) : AccessExpression;

    /// <summary>
    /// A literal value
    /// </summary>
    public sealed record Constant(AccessValue Value) : AccessExpression;

    public sealed record Arithmetic(ArithmeticOperator Operator, AccessExpression Left, AccessExpression Right) : AccessExpression;
}
