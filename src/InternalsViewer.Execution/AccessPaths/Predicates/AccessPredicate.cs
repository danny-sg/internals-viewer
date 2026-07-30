using System.Collections.Immutable;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;

/// <summary>
/// A predicate evaluated against a row, using three valued logic
/// </summary>
public abstract record AccessPredicate
{
    /// <summary>
    /// Always true, used when no residual predicate is present
    /// </summary>
    public sealed record True : AccessPredicate;

    /// <summary>
    /// A comparison between two scalar expressions
    /// </summary>
    public sealed record Comparison(AccessExpression Left,
                                    ComparisonOperator Operator,
                                    AccessExpression Right) : AccessPredicate;

    /// <summary>
    /// A conjunction of predicates
    /// </summary>
    public sealed record And(ImmutableArray<AccessPredicate> Predicates) : AccessPredicate;

    /// <summary>
    /// A disjunction of predicates
    /// </summary>
    public sealed record Or(ImmutableArray<AccessPredicate> Predicates) : AccessPredicate;

    /// <summary>
    /// A negation, where unknown remains unknown
    /// </summary>
    public sealed record Not(AccessPredicate Predicate) : AccessPredicate;

    /// <summary>
    /// A null test, which is never unknown
    /// </summary>
    public sealed record IsNull(AccessExpression Expression) : AccessPredicate;

    /// <summary>
    /// A membership test against a list of values
    /// </summary>
    public sealed record In(AccessExpression Expression,
                            ImmutableArray<AccessExpression> Values) : AccessPredicate;

    /// <summary>
    /// A pattern match against a string column
    /// </summary>
    public sealed record Like(AccessExpression Expression, string Pattern) : AccessPredicate;
}
