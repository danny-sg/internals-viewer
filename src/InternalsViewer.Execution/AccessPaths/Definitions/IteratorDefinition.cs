using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes what an iterator should do, everything it needs to open that does not depend on the database it runs against
/// </summary>
/// <remarks>
/// This is the resolved form of a plan operator rather than the plan operator itself. A showplan node names columns and objects, and
/// turning those into allocation units, root pages and seek bounds needs a database, so the trace builder does it once and hands the
/// result down. Keeping the plan out of here is also what lets Execution depend on Internals alone.
/// </remarks>
public abstract record IteratorDefinition
{
    /// <summary>
    /// A predicate applied to rows the iterator produces, after whatever access path it uses has found them
    /// </summary>
    public AccessPredicate? Residual { get; init; }

    /// <summary>
    /// Rows an operator above will ask for before it stops, when a TOP or a unique seek bounds the walk
    /// </summary>
    public long? RowGoal { get; init; }

    /// <summary>
    /// Part of the plan's predicate could not be translated, so the residual applied here is weaker than the real one
    /// </summary>
    public bool HasUntranslatedResidual { get; init; }

    /// <summary>
    /// Narrows this definition to the shape an iterator opens, failing loudly when it was given the wrong one
    /// </summary>
    public T Expect<T>()
        where T : IteratorDefinition
    {
        return this as T
               ?? throw new ArgumentException($"A {typeof(T).Name} is needed to open this iterator, but it was given a {GetType().Name}");
    }
}
