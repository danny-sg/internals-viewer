using InternalsViewer.Internals.Interfaces.DataAccess;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Binding;

/// <summary>
/// Binds record slots to a value source backed by the record fields
/// </summary>
public sealed class RecordRowBinder : IRowBinder
{
    public IRowValueSource Bind(IAccessPage page, int slot)
    {
        return Bind(page.GetRecord(slot));
    }

    public IRowValueSource Bind(IRecord record)
    {
        return new RecordRowValueSource(record);
    }
}
