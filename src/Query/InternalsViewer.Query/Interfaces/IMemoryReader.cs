namespace InternalsViewer.Query.Interfaces;

public interface IMemoryReader
{
    int Read(ulong address, Span<byte> destination);
}
