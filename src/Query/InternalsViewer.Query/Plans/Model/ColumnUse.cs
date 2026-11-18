using InternalsViewer.Query.Plans.Enums;

namespace InternalsViewer.Query.Plans.Model;

public sealed record ColumnUse(ColumnReference Column, ColumnUseType Type);
