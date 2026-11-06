using InternalsViewer.Query.Plans.Model;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed record PlanCounter(PlanNode Node, string Name, int Count, Color Colour);
