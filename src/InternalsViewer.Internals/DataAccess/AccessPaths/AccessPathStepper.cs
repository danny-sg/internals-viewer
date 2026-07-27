using InternalsViewer.Internals.DataAccess.AccessPaths.Results;

namespace InternalsViewer.Internals.DataAccess.AccessPaths;

/// <summary>
/// Drives an access path one step at a time, keeping the steps already taken
/// </summary>
/// <remarks>
/// Executors produce steps lazily, so nothing is evaluated until <see cref="MoveNext"/> is called.
/// This lets a caller advance the access path under user control and keeps the history needed to
/// render what has happened so far.
/// </remarks>
public sealed class AccessPathStepper(IEnumerable<AccessStep> steps)
{
    private IEnumerator<AccessStep>? Enumerator { get; set; }

    private IEnumerable<AccessStep> Steps { get; } = steps;

    private List<AccessStep> TakenSteps { get; } = [];

    /// <summary>
    /// Steps taken so far, in the order they occurred
    /// </summary>
    public IReadOnlyList<AccessStep> History => TakenSteps;

    /// <summary>
    /// The step the access path is currently sitting on, or null before the first step
    /// </summary>
    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    /// <summary>
    /// Totals as they stood after the current step
    /// </summary>
    public AccessCounters Counters => Current?.Counters ?? default;

    /// <summary>
    /// Whether the access path has produced its final step
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// Advances by a single step, returning false once no steps remain
    /// </summary>
    public bool MoveNext()
    {
        if (IsComplete)
        {
            return false;
        }

        Enumerator ??= Steps.GetEnumerator();

        if (!Enumerator.MoveNext())
        {
            IsComplete = true;

            return false;
        }

        TakenSteps.Add(Enumerator.Current);

        return true;
    }

    /// <summary>
    /// Advances until the access path stops, returning the number of steps taken
    /// </summary>
    public int RunToEnd()
    {
        var taken = 0;

        while (MoveNext())
        {
            taken++;
        }

        return taken;
    }

    /// <summary>
    /// Advances until a step of the given type is reached, returning it when found
    /// </summary>
    public TStep? RunTo<TStep>()
        where TStep : AccessStep
    {
        while (MoveNext())
        {
            if (Current is TStep step)
            {
                return step;
            }
        }

        return null;
    }

    /// <summary>
    /// Discards all progress so the access path can be replayed from the start
    /// </summary>
    public void Restart()
    {
        Enumerator?.Dispose();
        Enumerator = null;

        TakenSteps.Clear();

        IsComplete = false;
    }
}
