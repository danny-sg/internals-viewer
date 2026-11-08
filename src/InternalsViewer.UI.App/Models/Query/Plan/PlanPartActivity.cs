using InternalsViewer.Query.Plans.Model;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed record PlanPartActivity(PlanNode Node, string Part, Color Colour, bool IsRunning, double? Fraction, int Rank);
