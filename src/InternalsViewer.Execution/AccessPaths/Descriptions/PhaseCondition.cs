using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Text;

namespace InternalsViewer.Execution.AccessPaths.Descriptions;

/// <summary>
/// The predicate a phase reads out, where the operator it describes has one
/// </summary>
internal static class PhaseCondition
{
    public static bool Exists(AccessPredicate? residual)
        => residual is not (null or AccessPredicate.True or AccessPredicate.NoTranslation);

    public static ImmutableArray<PredicateToken> Of(AccessPredicate? residual)
        => Exists(residual) ? PredicateWriter.Write(residual!) : [];
}
