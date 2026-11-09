using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Query.Plan.Enums;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed record PlanStepMarker(PlanNode Node, PlanStepPlacement Placement);
