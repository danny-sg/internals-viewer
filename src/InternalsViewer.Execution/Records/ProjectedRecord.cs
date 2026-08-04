using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Records;

public sealed class ProjectedRecord : IRecord
{
    private ProjectedRecord(IRecord source, List<RecordField> fields)
    {
        Source = source;
        Fields = fields;
    }

    public IRecord Source { get; }

    public int Slot => Source.Slot;

    public ushort Offset => Source.Offset;

    public List<RecordField> Fields { get; }

    public short ColumnCount => (short)Fields.Count;

    public bool IsGhost => Source.IsGhost;

    public List<DataStructureItem> MarkItems => Source.MarkItems;

    public static IRecord Project(IRecord source, IReadOnlyList<OutputColumn> outputList)
    {
        if (outputList.Count == 0)
        {
            return source;
        }

        var fields = new List<RecordField>(outputList.Count);

        foreach (var column in outputList)
        {
            var field = source.Fields.FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName,
                                                                        column.Name,
                                                                        StringComparison.OrdinalIgnoreCase));

            if (field is not null)
            {
                fields.Add(field);
            }
        }

        return fields.Count == 0 ? source : new ProjectedRecord(source, fields);
    }

    public static IRecord Unwrap(IRecord record)
    {
        while (record is ProjectedRecord projected)
        {
            record = projected.Source;
        }

        return record;
    }
}
