namespace InternalsViewer.Execution.AccessPaths.Elimination;

public readonly record struct EliminationResult(bool IsEliminated, string Reason)
{
    public static EliminationResult Kept { get; } = new(false, string.Empty);

    public static EliminationResult Eliminated(string reason) => new(true, reason);
}
