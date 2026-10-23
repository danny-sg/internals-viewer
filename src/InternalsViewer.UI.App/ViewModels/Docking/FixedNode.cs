namespace InternalsViewer.UI.App.ViewModels.Docking;

public sealed class FixedNode : LayoutNode
{
    public double MinWidth { get; set; }

    public override double GetMinWidth(double splitterSize) => MinWidth;
}
