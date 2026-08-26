using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Records;

public sealed class BatchRecord(List<RecordField> fields, int rowGroupId, int position) : IRecord
{
    public int RowGroupId { get; } = rowGroupId;

    public int Position { get; } = position;

    public int Slot => -1;

    public ushort Offset => 0;

    public List<RecordField> Fields { get; } = fields;

    public short ColumnCount => (short)Fields.Count;

    public bool IsGhost => false;

    public List<DataStructureItem> MarkItems { get; } = [];
}
