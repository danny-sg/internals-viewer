namespace InternalsViewer.Query.Events.BatchMode.Enums;

public enum ColumnStoreInstructionSet
{
    NoSimd = 0,
    Sse42 = 1,
    Avx2 = 2,
    Avx512 = 3,
    Max = 6
}
