namespace InternalsViewer.Query.FullTrace.Hooks;

internal interface ITraceMemory
{
    int Read(ulong address, Span<byte> destination);
}
