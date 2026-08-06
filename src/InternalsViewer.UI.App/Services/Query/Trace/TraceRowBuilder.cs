using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.Services.Query.Trace;

/// <summary>
/// Builds the joined output row from the columns the join operator states it returns
/// </summary>
/// <remarks>
/// The operator's output list is what the join actually hands upwards, so it fixes both the columns and their order. Anything a side
/// read only to do its own work, a bookmark most of all, is left out. A column the preserved side of an outer join has no row for
/// reads as NULL, which also keeps the grid's columns steady from row to row.
/// </remarks>
public sealed class TraceRowBuilder(IReadOnlyDictionary<int, IteratorDefinition> definitions,
                                    IReadOnlyDictionary<int, OperatorSides> sides)
{
    public IndexRecordModel ToJoinedModel(AccessStep.JoinEmit emit)
    {
        var outputList = definitions.TryGetValue(emit.NodeId, out var definition) ? definition.OutputList : [];

        var fields = outputList.Count > 0
            ? [.. outputList.Select(c => ToField(emit, c))]
            : Combine(emit);

        return new IndexRecordModel
        {
            Slot = emit.OuterRecord?.Slot ?? emit.InnerRecord?.Slot ?? 0,
            RowIdentifier = null,
            Fields = fields
        };
    }

    private IndexRecordFieldModel ToField(AccessStep.JoinEmit emit, OutputColumn column)
    {
        var field = FindColumn(emit.OuterRecord, emit.InnerRecord, emit.NodeId, column.Table ?? string.Empty, column.Name);

        return new IndexRecordFieldModel
        {
            Name = column.Name,
            Value = field?.Value ?? "NULL",
            DataType = field?.ColumnStructure.DataType ?? column.DataType ?? SqlDbType.Variant
        };
    }

    /// <summary>
    /// Finds the column an operator states it outputs, following the side of the tree the table it belongs to sits on
    /// </summary>
    /// <remarks>
    /// A name alone cannot say which column is meant once an operator reads another - a join of two tables that both have a Name hands up
    /// a row carrying both, and the operator above states which of them it wants by naming the table. The tables under each side are known
    /// from the definition tree, so the side is settled first and the name resolved within it.
    /// </remarks>
    private RecordField? FindColumn(IRecord? outer, IRecord? inner, int operatorNodeId, string table, string name)
    {
        if (table.Length > 0 && sides.TryGetValue(operatorNodeId, out var operatorSides))
        {
            if (operatorSides.OuterTables.Contains(table))
            {
                return Descend(outer, operatorSides.OuterNodeId, table, name)
                       ?? Descend(inner, operatorSides.InnerNodeId, table, name);
            }

            if (operatorSides.InnerTables.Contains(table))
            {
                return Descend(inner, operatorSides.InnerNodeId, table, name)
                       ?? Descend(outer, operatorSides.OuterNodeId, table, name);
            }
        }

        return Find(outer, name) ?? Find(inner, name);
    }

    private RecordField? Descend(IRecord? record, int operatorNodeId, string table, string name)
        => record is JoinRecord joined
            ? FindColumn(joined.Outer, joined.Inner, operatorNodeId, table, name)
            : Find(record, name);

    private static List<IndexRecordFieldModel> Combine(AccessStep.JoinEmit emit)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fields = new List<IndexRecordFieldModel>();

        foreach (var record in new[] { emit.OuterRecord, emit.InnerRecord })
        {
            foreach (var field in record?.Fields ?? [])
            {
                fields.Add(new IndexRecordFieldModel
                {
                    Name = names.Add(field.Name) ? field.Name : $"Inner.{field.Name}",
                    Value = field.Value,
                    DataType = field.ColumnStructure.DataType
                });
            }
        }

        return fields;
    }

    private static RecordField? Find(IRecord? record, string name)
        => record?.Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
}
