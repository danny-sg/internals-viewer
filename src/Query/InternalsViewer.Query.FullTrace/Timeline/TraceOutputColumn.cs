using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOutputColumn(int Ordinal, RowBufferColumn? Value, RowBufferSource? Source)
{
    public bool IsFromDictionary => Source?.IsFromDictionary == true;
}
