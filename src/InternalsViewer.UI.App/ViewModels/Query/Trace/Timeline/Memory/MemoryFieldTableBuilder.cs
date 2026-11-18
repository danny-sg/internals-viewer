using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

internal static class MemoryFieldTableBuilder
{
    public const int FieldWidth = 260;

    public const int OffsetWidth = 80;

    public const int ValueWidth = 320;

    public const int IndexWidth = 60;

    private const int RowFieldWidth = 140;

    private const string IndexHeader = "Index";

    private const int HueWheel = 256;

    private const double GoldenRatio = 0.6180339887;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    public static List<Colour> CreateColours(int count)
        => [.. Enumerable.Range(0, count).Select(i => ColourHelpers.HsvToColor((int)(i * GoldenRatio * HueWheel) % HueWheel,
                                                                               FieldSaturation,
                                                                               FieldValue))];

    public static Marker CreateMarker(string name, int offset, int length, Colour colour) => new()
    {
        Name = name,
        StartPosition = offset,
        EndPosition = offset + length - 1,
        BackColour = colour.ToWindowsColor(),
        MapColour = colour.ToWindowsColor(),
        ForeColour = Colors.Black
    };

    public static MemoryObjectTab CreateTab(string header, QueryResultSet table, string key, Action<ResultRow<long>?>? select = null)
        => new(header, [], select) { Table = table, ValueKey = key };

    public static QueryResultSet CreateFieldTable(EngineLayout layout,
                                                  Func<ulong, object> getPointerCell,
                                                  List<MemoryField> fields,
                                                  ulong?[] values,
                                                  List<Colour> colours,
                                                  Func<ulong, int, string>? getBytes = null)
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Field", typeof(string), false) { Width = FieldWidth },
            new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
            new(2, "Value", typeof(string), true) { Width = ValueWidth, IsStar = true }
        };

        var rows = fields.Select((f, i) => new ResultRow<long>([new SwatchText(f.Name, colours[i]),
                                                                 $"0x{f.Offset:X}",
                                                                 GetValueCell(layout, getPointerCell, f, values[i], getBytes)])
        {
            Id = i
        });

        return new QueryResultSet { Columns = columns, Rows = [.. rows] };
    }

    public static QueryResultSet CreateRowTable(EngineLayout layout,
                                                Func<ulong, object> getPointerCell,
                                                LayoutObject item,
                                                List<Colour> colours,
                                                Func<ulong, int, string>? getBytes = null)
    {
        var columns = new List<ResultColumn>
        {
            new(0, IndexHeader, typeof(string), false) { Width = IndexWidth, Alignment = ResultAlignment.Right }
        };

        columns.AddRange(item.Fields.Select((f, i) => new ResultColumn(i + 1, f.Name, typeof(string), true)
        {
            Width = RowFieldWidth,
            MarkerColour = colours[i]
        }));

        var rows = item.Rows.Select((r, i) => new ResultRow<long>(
        [
            $"{i}",
            .. item.Fields.Select((f, j) => GetValueCell(layout, getPointerCell, f, r[j], getBytes))
        ])
        {
            Id = i
        });

        return new QueryResultSet { Columns = columns, Rows = [.. rows] };
    }

    public static object GetValueCell(EngineLayout layout,
                                      Func<ulong, object> getPointerCell,
                                      MemoryField field,
                                      ulong? value,
                                      Func<ulong, int, string>? getBytes = null)
    {
        if (value is not { } found)
        {
            return string.Empty;
        }

        if (field.IsBytes)
        {
            return getBytes?.Invoke(found, field.Length) ?? string.Empty;
        }

        if (field.Decoder is { } decoder)
        {
            return MemoryFieldDecoderLookup.GetText(layout, decoder, found);
        }

        if (field.Structure is not null)
        {
            return getPointerCell(found);
        }

        if (field.IsHex)
        {
            return $"0x{found:X8}";
        }

        return field.IsPointer ? getPointerCell(found) : $"{found}";
    }

    public static string GetStructure(List<MemoryObjectTab> tabs)
        => string.Join(";", tabs.Select(t => $"{t.Header}[{GetStructure(t.Tabs)}]"));

    public static void Update(List<MemoryObjectTab> current, List<MemoryObjectTab> next)
    {
        for (var index = 0; index < current.Count; index++)
        {
            if (current[index].ValueKey != next[index].ValueKey)
            {
                current[index].Table = next[index].Table;

                current[index].ValueKey = next[index].ValueKey;
            }

            Update(current[index].Tabs, next[index].Tabs);
        }
    }
}
