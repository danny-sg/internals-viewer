namespace InternalsViewer.Query.FullTrace.Export;

internal sealed record ExportPlan(int Index, short PlanHandleId, bool IsInternalPlan, string File, string? BatchFile);
