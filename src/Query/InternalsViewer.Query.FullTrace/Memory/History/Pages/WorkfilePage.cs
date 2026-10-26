using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;

namespace InternalsViewer.Query.FullTrace.Memory.History.Pages;

public sealed record WorkfilePage(DataPage Page, List<FixedVarRecord> Records, RowRecordLayout? Layout)
{
    public bool HasValues => Records.Any(r => r.Fields.Count > 0);
}
