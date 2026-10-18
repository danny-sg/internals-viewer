using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Output.Sources;

internal sealed record UnknownOutputSource() : TraceOutputSource(0UL)
{
    internal override OutputSourceType Type => OutputSourceType.Unknown;

    internal override RowBufferColumn? GetColumn(RowBuffer row) => null;
}
