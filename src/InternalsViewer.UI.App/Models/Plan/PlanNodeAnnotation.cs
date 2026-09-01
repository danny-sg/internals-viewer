using Windows.UI;

namespace InternalsViewer.UI.App.Models.Plan;

public sealed record PlanNodeAnnotation(string Text, string Detail, Color Colour)
{
    public string ToolTip => Detail.Length > 0 ? $"{Text} - {Detail}" : Text;
}
