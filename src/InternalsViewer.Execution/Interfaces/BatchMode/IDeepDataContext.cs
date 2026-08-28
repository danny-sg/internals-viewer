namespace InternalsViewer.Execution.Interfaces.BatchMode;

public interface IDeepDataContext
{
    int Count { get; }

    long Store(ReadOnlySpan<byte> value);

    ReadOnlySpan<byte> Get(long slot);

    long AddressOf(int index);

    void Clear();
}