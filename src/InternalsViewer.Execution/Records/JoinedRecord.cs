using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Records;

/// <summary>
/// The single row a join hands upwards, carrying the columns of both sides
/// </summary>
/// <remarks>
/// An operator reading a join has to see one row, not a pair, because it hashes or compares columns without caring which side they came
/// from. Both sides are kept whole rather than merged by name, because a column each side carries is two different columns and dropping
/// one loses it for everything above - a join of two tables that both have a Name can still be asked for either. A consumer resolving a
/// column by name takes the first it finds, which is the outer one, so a name that is ambiguous resolves as it always did.
/// </remarks>
public sealed class JoinedRecord : IRecord
{
    private JoinedRecord(IRecord outer, IRecord inner, List<RecordField> fields)
    {
        Outer = outer;
        Inner = inner;
        Fields = fields;
    }

    /// <summary>
    /// The row this side of the join contributed, kept so that two columns of the same name can be told apart
    /// </summary>
    public IRecord Outer { get; }

    public IRecord Inner { get; }

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

        return new JoinedRecord(outer, inner, [.. outer.Fields, .. inner.Fields]);
    }
}
