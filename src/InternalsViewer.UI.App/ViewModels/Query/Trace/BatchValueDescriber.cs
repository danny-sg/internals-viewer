using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.UI.App.Models.Query.Trace.Batch;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

/// <summary>
/// Describes what one batch slot holds, from the raw normalized value through to the value it decodes to
/// </summary>
public static class BatchValueDescriber
{
    public static IReadOnlyList<BatchDetailItem> Describe(BatchColumnView column,
                                                          BatchRowView row,
                                                          IReadOnlyList<BatchDeepDataRow> deepData)
    {
        var slot = row.Values[column.Ordinal];

        var valueType = BatchValueDenormalizer.GetValueType(slot, column.Column);

        var items = new List<BatchDetailItem>
        {
            Item("Index", row.RowIndex.ToString(CultureInfo.InvariantCulture)),
            Item("Selected", row.IsSelected ? "Yes" : "No"),
            Hex("Slot", slot.Value),
            Item("Slot Value", slot.Value.ToString(CultureInfo.InvariantCulture)),
            Item("Kind", SplitWords(valueType.ToString())),
            Item("Data Type", column.DataType.ToString()),
            Item("Domain", column.Domain.ToString())
        };

        switch (valueType)
        {
            case BatchValueType.Null:
                items.Add(Item("Value", "NULL"));

                break;

            case BatchValueType.Inline:
                items.Add(Item("Value", InlineValue(column, slot)));

                break;

            case BatchValueType.DictionaryReference:
                AddDictionary(items, column, slot);

                break;

            case BatchValueType.DeepDataReference:
                AddDeepData(items, slot, deepData);

                break;
        }

        AddSegment(items, column);

        return items;
    }

    public static IReadOnlyList<BatchDetailItem> DescribeDeepData(BatchDeepDataRow row)
    {
        var items = new List<BatchDetailItem>
        {
            new() { Name = "Address", Value = row.AddressText, IsMonospaced = true },
            Item("Length", row.Length.ToString(CultureInfo.InvariantCulture)),
            new() { Name = "Data", Value = row.Data, IsMonospaced = true },
            Item("Text", AsText(row.Data)),
            Item("ASCII", AsAscii(row.Data))
        };

        return items;
    }

    private static string AsAscii(string hex)
    {
        try
        {
            var bytes = Convert.FromHexString(hex);

            return string.Concat(bytes.Select(b => b is >= 32 and < 127 ? (char)b : '.'));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static void AddDictionary(List<BatchDetailItem> items, BatchColumnView column, BatchValue slot)
    {
        var dataId = BatchValueDenormalizer.GetDictionaryDataId(slot);

        items.Add(Item("Data Id", dataId.ToString(CultureInfo.InvariantCulture)));

        if (column.Source is not { } source)
        {
            items.Add(Item("Dictionary", "Not Attached"));

            return;
        }

        items.Add(Item("Dictionary", DictionaryName(column)));

        try
        {
            items.Add(Item("Dictionary Value", Format(source.GetValueForDataId(dataId))));
        }
        catch (Exception exception)
        {
            items.Add(Item("Dictionary Value", exception.Message));
        }
    }

    private static void AddDeepData(List<BatchDetailItem> items, BatchValue slot, IReadOnlyList<BatchDeepDataRow> deepData)
    {
        var index = (int)(slot.Value >> 1) - 1;

        items.Add(Item("Deep Data Index", index.ToString(CultureInfo.InvariantCulture)));

        if (index < 0 || index >= deepData.Count)
        {
            items.Add(Item("Deep Data", "Not Found"));

            return;
        }

        var entry = deepData[index];

        items.Add(Item("Length", entry.Length.ToString(CultureInfo.InvariantCulture)));

        items.Add(new BatchDetailItem { Name = "Data", Value = entry.Data, IsMonospaced = true });

        items.Add(Item("Text", AsText(entry.Data)));
    }

    private static void AddSegment(List<BatchDetailItem> items, BatchColumnView column)
    {
        if (column.Source?.Segment is not { } segment)
        {
            return;
        }

        items.Add(Item("Encoding", SplitWords(segment.Encoding.ToString())));

        items.Add(Item("Min Data Id", segment.MinDataId.ToString(CultureInfo.InvariantCulture)));

        items.Add(Item("Max Data Id", segment.MaxDataId.ToString(CultureInfo.InvariantCulture)));

        if (segment.BaseId != 0)
        {
            items.Add(Item("Base Id", segment.BaseId.ToString(CultureInfo.InvariantCulture)));
        }

        if (Math.Abs(segment.Magnitude - 1) > double.Epsilon)
        {
            items.Add(Item("Magnitude", segment.Magnitude.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string DictionaryName(BatchColumnView column)
    {
        var segment = column.Source?.Segment;

        if (segment is null)
        {
            return "Not Attached";
        }

        return segment.LocalDictionary is not null
            ? $"Local ({segment.PrimaryDictionaryId})"
            : $"Global ({segment.PrimaryDictionaryId})";
    }

    private static string InlineValue(BatchColumnView column, BatchValue slot)
    {
        try
        {
            return column.Domain switch
            {
                BatchValueDomain.Temporal => Format(BatchValueDenormalizer.GetTemporalValue(slot, column.Column)),
                BatchValueDomain.Real => BitConverter.Int64BitsToDouble(BatchValueDenormalizer.GetStorageValue(slot, column.Column))
                                                    .ToString(CultureInfo.InvariantCulture),
                _ => BatchValueDenormalizer.GetStorageValue(slot, column.Column).ToString(CultureInfo.InvariantCulture)
            };
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private static string AsText(string hex)
    {
        try
        {
            var bytes = Convert.FromHexString(hex);

            return System.Text.Encoding.Unicode.GetString(bytes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string Format(object? value) => value switch
    {
        null => "NULL",
        byte[] bytes => Convert.ToHexString(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string SplitWords(string value) => Helpers.StringExtensions.SplitString(value);

    private static BatchDetailItem Item(string name, string value) => new() { Name = name, Value = value };

    private static BatchDetailItem Hex(string name, long value)
        => new() { Name = name, Value = $"0x{value:X16}", IsMonospaced = true };
}
