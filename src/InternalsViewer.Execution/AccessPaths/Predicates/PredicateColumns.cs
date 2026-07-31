namespace InternalsViewer.Execution.AccessPaths.Predicates;

/// <summary>
/// Collects the columns a predicate reads
/// </summary>
public static class PredicateColumns
{
    public static IEnumerable<string> Referenced(AccessPredicate predicate)
        => predicate switch
        {
            AccessPredicate.Comparison comparison => Referenced(comparison.Left).Concat(Referenced(comparison.Right)),
            AccessPredicate.And and => and.Predicates.SelectMany(Referenced),
            AccessPredicate.Or or => or.Predicates.SelectMany(Referenced),
            AccessPredicate.Not not => Referenced(not.Predicate),
            AccessPredicate.IsNull isNull => Referenced(isNull.Expression),
            AccessPredicate.In inList => Referenced(inList.Expression).Concat(inList.Values.SelectMany(Referenced)),
            AccessPredicate.Like like => Referenced(like.Expression),
            _ => []
        };

    public static IEnumerable<string> Referenced(AccessExpression expression)
        => expression switch
        {
            AccessExpression.Column column => [column.Name],
            AccessExpression.Arithmetic arithmetic => Referenced(arithmetic.Left).Concat(Referenced(arithmetic.Right)),
            AccessExpression.Function function => function.Arguments.SelectMany(Referenced),
            AccessExpression.Aggregate aggregate => aggregate.Arguments.SelectMany(Referenced),
            AccessExpression.Conditional conditional => Referenced(conditional.Condition)
                                                        .Concat(Referenced(conditional.Then))
                                                        .Concat(Referenced(conditional.Else)),
            _ => []
        };
}
