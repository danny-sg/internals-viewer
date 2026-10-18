using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOutputColumn(int Ordinal, RowBuffer? Row, RowBufferColumn? Value, RowBufferSource? Source)
{
    public bool IsFromDictionary => Source?.IsFromDictionary == true;
}
