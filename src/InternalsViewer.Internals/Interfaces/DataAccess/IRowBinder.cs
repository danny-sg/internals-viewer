using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Interfaces.DataAccess;

/// <summary>
/// Binds a slot on a page to a value source so residual predicates can be evaluated
/// </summary>
public interface IRowBinder
{
    IRowValueSource Bind(IAccessPage page, int slot);

    IRowValueSource Bind(IRecord record);
}
