namespace InternalsViewer.UI.App.Controls.Query.Timeline;

internal readonly record struct TimelineRulerArea(double Left, double Right)
{
    public double Width => Right - Left;
}
