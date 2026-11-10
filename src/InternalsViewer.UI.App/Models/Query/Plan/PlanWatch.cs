using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed record PlanWatch(PlanNode Node, string Label, string Value);
