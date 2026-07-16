namespace InternalsViewer.UI.App.Controls.Plan;

/// <summary>One box in an operator's mini icicle: a call-stack frame sized by its share of the operator's events</summary>
public sealed record IcicleSegment(double X, double Y, double Width, double Height, string Colour, string Symbol);
