using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Operators;

public static class InputLabel
{
    public const string Build = "Build";

    public const string Probe = "Probe";

    public const string OuterLoop = "Outer Loop";

    public const string InnerLoop = "Inner Loop";

    public const string Outer = "Outer";

    public const string Inner = "Inner";

    public static string? Get(PlanNode parent, PlanNode child)
    {
        if (OperatorClassifier.IsHashJoin(parent))
        {
            return OperatorClassifier.RoleOf(parent, child) == InputRole.Blocking ? Build : Probe;
        }

        var index = parent.Children.IndexOf(child);

        if (OperatorClassifier.IsNestedLoop(parent))
        {
            return index switch { 0 => OuterLoop, 1 => InnerLoop, _ => null };
        }

        return OperatorClassifier.IsMergeJoin(parent) ? index switch { 0 => Outer, 1 => Inner, _ => null } : null;
    }
}
