using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Joins;

public sealed record BitmapProbe(PlanNode Creator, PlanNode Probe, string Bitmap);
