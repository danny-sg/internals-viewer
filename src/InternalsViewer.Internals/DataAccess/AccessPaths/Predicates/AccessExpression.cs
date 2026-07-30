using System.Collections.Immutable;
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

    public sealed record Function(string Name, ImmutableArray<AccessExpression> Arguments) : AccessExpression
    {
        public bool Equals(Function? other)
        {
            return other is not null
                   && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                   && Arguments.SequenceEqual(other.Arguments);
        }

        public override int GetHashCode()
        {
            var hash = default(HashCode);

            hash.Add(Name, StringComparer.OrdinalIgnoreCase);

            foreach (var argument in Arguments)
            {
                hash.Add(argument);
            }

            return hash.ToHashCode();
        }
    }

    public sealed record Conditional(AccessPredicate Condition, AccessExpression Then, AccessExpression Else) : AccessExpression;
}
