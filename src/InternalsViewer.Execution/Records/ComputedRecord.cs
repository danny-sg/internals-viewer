using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Records;

public sealed class ComputedRecord : IRecord
{
    private ComputedRecord(IRecord? source, List<RecordField> fields)
    {
        Source = source;
        Fields = fields;
    }

    public IRecord? Source { get; }

    public int Slot => Source?.Slot ?? -1;

    public ushort Offset => Source?.Offset ?? 0;

    public List<RecordField> Fields { get; }

    public short ColumnCount => (short)Fields.Count;

    public bool IsGhost => false;

    public List<DataStructureItem> MarkItems { get; } = [];

    public static ComputedRecord Extend(IRecord source, IEnumerable<RecordField> computed)
        => new(source, [.. source.Fields, .. computed]);

    public static ComputedRecord Create(IEnumerable<RecordField> fields)
        => new(null, [.. fields]);
}
