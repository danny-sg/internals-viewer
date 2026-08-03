using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Records;

/// <summary>
/// The single row a join hands upwards, carrying the columns of both sides
/// </summary>
/// <remarks>
/// An operator reading a join has to see one row, not a pair, because it hashes or compares columns without caring which side they came
/// from. Where both sides carry the same column name the outer one wins, matching the order a join states its output in.
/// </remarks>
public sealed class JoinedRecord : IRecord
{
    private JoinedRecord(List<RecordField> fields)
    {
        Fields = fields;
    }

    public int Slot => -1;

    public ushort Offset => 0;

    public List<RecordField> Fields { get; }

    public short ColumnCount => (short)Fields.Count;

    public bool IsGhost => false;

    public List<DataStructureItem> MarkItems { get; } = [];

    /// <summary>
    /// Combines the two sides of a pair, or returns the only side there is when the join preserved a row with no partner
    /// </summary>
    public static IRecord? Combine(IRecord? outer, IRecord? inner)
    {
        if (outer is null)
        {
            return inner;
        }

        if (inner is null)
        {
            return outer;
        }

        var fields = new List<RecordField>(outer.Fields);

        var names = outer.Fields
                         .Select(f => f.ColumnStructure.ColumnName)
                         .ToHashSet(StringComparer.OrdinalIgnoreCase);

        fields.AddRange(inner.Fields.Where(f => names.Add(f.ColumnStructure.ColumnName)));

        return new JoinedRecord(fields);
    }
}
