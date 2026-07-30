namespace InternalsViewer.Execution.AccessPaths.Predicates;

public sealed record EvaluationContext(DateTime QueryTime)
{
    public static EvaluationContext Now => new(DateTime.Now);
}
