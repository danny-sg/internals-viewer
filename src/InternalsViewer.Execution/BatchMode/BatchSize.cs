namespace InternalsViewer.Execution.BatchMode;

/// <summary>
/// Batch Size calculations
/// </summary>
/// <remarks>
/// Batches are 64 KB, sized to fit comfortably in the L2 CPU cache and to divide evenly into 64-byte cache lines and 512-bit AVX-512
/// registers, so vector operations can stream through the batch with minimal cache misses.
///
/// Each value in a batch vector is 64 bits (8 bytes), so the row count is the number of rows of (column count * 8 bytes) that fit in 64 KB,
/// clamped between 64 and 900. The 64-bit values are cache line aligned without straddling boundaries, keeping sequential memory throughput
/// high.
/// </remarks>
public static class BatchSize
{
    public const int MaxBytes = 65536;

    public const int MinRowCount = 64;

    public const int MaxRowCount = 900;

    private const int ValueBytes = 8;

    public static int GetRowCount(int columnCount)
        => columnCount <= 0
            ? MaxRowCount
            : Math.Clamp(MaxBytes / (ValueBytes * columnCount), MinRowCount, MaxRowCount);
}
