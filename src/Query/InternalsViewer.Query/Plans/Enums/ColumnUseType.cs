namespace InternalsViewer.Query.Plans.Enums;

[Flags]
public enum ColumnUseType : byte
{
    None = 0,
    Expression = 1,
    BuildKey = 2,
    ProbeKey = 4,
    Predicate = 8,
    Group = 16,
    Order = 32
}
