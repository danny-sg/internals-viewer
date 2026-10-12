namespace InternalsViewer.Query.Export;

internal sealed record ExportPlan(int Index, short PlanHandleId, bool IsInternalPlan, string File, string? BatchFile);
