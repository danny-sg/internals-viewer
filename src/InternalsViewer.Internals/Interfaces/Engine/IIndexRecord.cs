using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Annotations;

namespace InternalsViewer.Internals.Interfaces.Engine;

public interface IIndexRecord : IRecord
{
    public PageAddress DownPagePointer { get; }

    public RowIdentifier? Rid { get; }

    public bool IncludeKey { get; }

    public NodeType NodeType { get; }
}

public interface IRecord : IDataStructure
{
    public int Slot { get; }

    public ushort Offset { get; }

    public List<RecordField> Fields { get; }

    public short ColumnCount { get; }

    /// <summary>
    /// Indicates the record is marked for deletion but has not yet been removed
    /// </summary>
    /// <remarks>
    /// The record type is format specific, so each record format decides which of its types are
    /// ghosts.
    /// </remarks>
    public bool IsGhost { get; }
}
