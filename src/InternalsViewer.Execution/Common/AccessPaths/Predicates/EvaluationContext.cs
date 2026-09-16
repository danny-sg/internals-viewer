namespace InternalsViewer.Execution.Common.AccessPaths.Predicates;

public sealed record EvaluationContext(DateTime QueryTime)
{
    public static EvaluationContext Now => new(DateTime.Now);
}
