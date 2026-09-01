using InternalsViewer.Execution.Interfaces.BatchMode;

namespace InternalsViewer.Execution.BatchMode;

/// <summary>
/// Batch scoped deep data memory storage
/// </summary>
/// <remarks>
/// Batch Vectors are 64-bit, using 1 bit as a flag leaving 63-bits for the normalized value. If a value requires more than 63-bits or it is
/// a known deep data type it is stored in a Deep Data memory arena and the vector uses an address with the low bit set to signify it is
/// deep data.
///
/// During batch processing the flag is used to determine how the value should be interpreted:
///
///     Value = 0, Flag = 1  -> NULL
///     Value != 0, Flag = 1 -> Deep Data Pointer
///     Flag = 0             -> Literal Value (Normalized value or Dictionary Id)
///
/// For the purpose of the Internals Viewer Execution this replicates that behaviour by storing the data in the Values array returning the
/// index shifted left by 1 as a pseudo address.
/// </remarks>
public sealed class BatchDeepDataStore : IDeepDataContext
{
    public int Count => Values.Count;

    public long ByteCount { get; private set; }

    private List<byte[]> Values { get; } = [];

    public long Store(ReadOnlySpan<byte> value)
    {
        Values.Add([.. value]);

        ByteCount += value.Length;

        return ((long)Values.Count << 1) | 1;
    }

    public ReadOnlySpan<byte> Get(long slot) => Values[(int)(slot >> 1) - 1];

    public long AddressOf(int index) => ((long)(index + 1) << 1) | 1;

    public void Clear()
    {
        Values.Clear();

        ByteCount = 0;
    }
}