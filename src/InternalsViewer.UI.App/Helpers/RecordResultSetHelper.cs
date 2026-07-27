using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Query.Results;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.UI.App.Helpers;

internal static class RecordResultSetHelper
{
    public static QueryResultSet ToResultSet(List<IRecord> records)
    {
        if (records.Count == 0)
        {
            return new QueryResultSet();
        }

        var template = records.MaxBy(r => r.Fields.Count)!;

        int? ridColumn = null;
        int? downPagePointerColumn;

        var columns = template.Fields
                              .Select((f, index) =>
                              {
                                  if (f.ColumnStructure is IndexColumnStructure { IsRowIdentifier: true })
                                  {
                                      ridColumn = index;

                                      return new ResultColumn(index + 1, f.Name, typeof(RowIdentifier), true)
                                      {
                                          Alignment = ResultAlignment.Right,
                                          Width = 100,
                                          BackgroundColour = Color.FromArgb(20, Color.LightBlue),
                                      };
                                  }

                                  return new ResultColumn(index + 1, f.Name, typeof(string), true);
                              });

        var slotColumn = new ResultColumn(0, "Slot", typeof(short), false)
        {
            BackgroundColour = Color.FromArgb(28, Color.Gainsboro),
            Width = 80
        };

        List<ResultColumn> resultColumns =
        [
            slotColumn,
            .. columns
        ];

        var hasDownPagePointer = false;

        if (template is IIndexRecord irx && irx.NodeType != NodeType.Leaf)
        {
            resultColumns.Add(new ResultColumn(resultColumns.Count, "Down Page Pointer", typeof(PageAddress), true)
            {
                Alignment = ResultAlignment.Right,
                Width = 140
            });

            hasDownPagePointer = true;
        }

        var rows = records.Select(r =>
        {
            var values = new object?[resultColumns.Count];

            values[0] = r.Slot;

            var indexRecord = r as IIndexRecord;


            for (var i = 0; i < r.Fields.Count; i++)
            {
                if (i == ridColumn && indexRecord is not null)
                {
                    values[i + 1] = indexRecord.Rid;
                }
                else
                {
                    values[i + 1] = r.Fields[i].Value;
                }
            }

            if (hasDownPagePointer && indexRecord is not null)
            {
                values[resultColumns.Count - 1] = indexRecord.DownPagePointer;
            }

            return new ResultRow<long>(values) { Id = r.Slot };
        });

        return new QueryResultSet
        {
            Columns = resultColumns,
            Rows = rows.ToList()
        };
    }
}
