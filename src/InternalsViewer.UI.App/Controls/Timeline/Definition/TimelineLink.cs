using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Controls.Timeline.Definition;

public readonly record struct TimelineLink(int Source,
                                           int TargetItem,
                                           PlanNodeIdentifier? TargetOperator,
                                           TimelineLinkStyle Style,
                                           int RailCount);

public enum TimelineLinkStyle : byte
{
    CallReturn,
    Bar,
}
