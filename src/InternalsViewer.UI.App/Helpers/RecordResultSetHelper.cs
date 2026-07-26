using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Query.Results;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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

        var columns = template.Fields
                              .Select((f, index) => new ResultColumn(index + 1, f.Name, typeof(string), true));

        var slotColumn = new ResultColumn(0, "Slot", typeof(short), false)
            { BackgroundColour = Color.FromArgb(28, Color.Gainsboro) };

        List<ResultColumn> resultColumns =
        [
            slotColumn,
            .. columns
        ];

        var rows = records.Select(r =>
        {
            var values = new object[resultColumns.Count];

            values[0] = r.Slot;

            for (var i = 0; i < r.Fields.Count; i++)
            {
                values[i + 1] = r.Fields[i].Value;
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
