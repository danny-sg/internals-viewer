namespace InternalsViewer.Execution.Interfaces.BatchMode;

public interface IDeepDataContext
{
    long Store(ReadOnlySpan<byte> value);

    ReadOnlySpan<byte> Get(long slot);

    void Clear();
}