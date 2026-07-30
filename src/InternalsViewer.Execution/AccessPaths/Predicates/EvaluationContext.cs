namespace InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;

public sealed record EvaluationContext(DateTime QueryTime)
{
    public static EvaluationContext Now => new(DateTime.Now);
}
