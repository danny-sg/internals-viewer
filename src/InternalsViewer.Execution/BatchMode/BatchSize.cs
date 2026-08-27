namespace InternalsViewer.Execution.BatchMode;

public static class BatchSize
{
    public const int MaxBytes = 65536;

    public const int MinRowCount = 64;

    public const int MaxRowCount = 900;

    private const int SlotBytes = 8;

    public static int GetRowCount(int columnCount)
        => columnCount <= 0
            ? MaxRowCount
            : Math.Clamp(MaxBytes / (SlotBytes * columnCount), MinRowCount, MaxRowCount);
}
