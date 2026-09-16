namespace InternalsViewer.Execution.BatchMode.Interfaces;

public interface IDeepDataContext
{
    int Count { get; }

    long ByteCount { get; }

    long Store(ReadOnlySpan<byte> value);

    ReadOnlySpan<byte> Get(long slot);

    long AddressOf(int index);

    void Clear();
}