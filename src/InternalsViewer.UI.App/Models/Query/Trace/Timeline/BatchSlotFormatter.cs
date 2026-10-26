using System;
using System.Data;
using System.Globalization;
using System.Text;
using InternalsViewer.Internals.BatchMode.Normalization;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

internal static class BatchSlotFormatter
{
    public static SqlDbType[] Types { get; } =
    [
        SqlDbType.BigInt,
        SqlDbType.Int,
        SqlDbType.SmallInt,
        SqlDbType.TinyInt,
        SqlDbType.Bit,
        SqlDbType.Float,
        SqlDbType.Real,
        SqlDbType.Date,
        SqlDbType.DateTime2,
        SqlDbType.Time,
        SqlDbType.DateTimeOffset,
        SqlDbType.VarChar,
        SqlDbType.NVarChar
    ];

    public static string Format(ulong raw, SqlDbType? type)
    {
        if (type is not { } dataType)
        {
            return $"0x{raw:X16}";
        }

        var slot = new BatchValue((long)raw);

        var domain = BatchValueNormalizer.GetDomain(dataType);

        try
        {
            return BatchValueDenormalizer.GetValueType(slot, domain) switch
            {
                BatchValueType.Null
                    => "NULL",
                BatchValueType.DeepDataReference
                    => $"Deep Data 0x{raw & ~1UL:X}",
                BatchValueType.DictionaryReference
                    => $"Dictionary {BatchValueDenormalizer.GetDictionaryNumber(slot)} "
                       + $"Entry {BatchValueDenormalizer.GetDictionaryEntry(slot)}",
                _ => FormatInline(slot, dataType, domain)
            };
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    public static string FormatDeep(byte[] data, SqlDbType? type) => type switch
    {
        SqlDbType.VarChar or SqlDbType.Char
            => Encoding.Latin1.GetString(data),
        SqlDbType.NVarChar or SqlDbType.NChar
            => Encoding.Unicode.GetString(data),
        _ => $"0x{Convert.ToHexString(data)}"
    };

    private static string FormatInline(BatchValue slot, SqlDbType dataType, BatchValueDomain domain) => domain switch
    {
        BatchValueDomain.Temporal
            => FormatTemporal(BatchValueDenormalizer.GetTemporalValue(slot, dataType)),
        BatchValueDomain.Real
            => BitConverter.Int64BitsToDouble(BatchValueDenormalizer.GetStorageValue(slot, domain)).ToString(CultureInfo.InvariantCulture),
        _ => BatchValueDenormalizer.GetStorageValue(slot, domain).ToString(CultureInfo.InvariantCulture)
    };

    private static string FormatTemporal(object value) => value switch
    {
        DateOnly date
            => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dateTime
            => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeOffset offset
            => offset.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture),
        TimeSpan time
            => time.ToString("c", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
