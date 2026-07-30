using InternalsViewer.Internals.DataAccess.AccessPaths.Binding;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Interfaces.DataAccess;

public interface IRowPageAccessor
{
    PageAddress PageAddress { get; }

    /// <summary>
    /// Index level, where zero is the leaf. A heap data page is always level zero
    /// </summary>
    byte Level { get; }

    bool IsLeaf { get; }

    int SlotCount { get; }

    /// <summary>
    /// Gets the underlying record for a slot, used when evaluating residual predicates
    /// </summary>
    IRecord GetRecord(int slot);

    IRowValueSource BindRow(int slot) => new RecordRowValueSource(GetRecord(slot));
}
