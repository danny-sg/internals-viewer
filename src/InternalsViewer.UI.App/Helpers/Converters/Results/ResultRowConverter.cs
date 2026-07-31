using System;
using InternalsViewer.Query.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Results;

internal sealed class ResultRowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var ordinal = parameter switch
        {
            int i => i,
            short s => s,
            long l => (int)l,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => -1
        };

        if (value is not ResultRow<long> row || ordinal < 0 || ordinal >= row.FieldCount)
        {
            return string.Empty;
        }

        return FormatValue(row[ordinal]);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();

    internal static string FormatValue(object? v)
    {
        if (v is null)
        {
            return "NULL";
        }

        return v switch
        {
            bool b 
                => b ? "1" : "0",
            byte[] bytes 
                => "0x" + System.Convert.ToHexString(bytes),
            DateTime dt 
                => dt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            DateTimeOffset dto 
                => dto.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
            TimeSpan ts 
                => ts.ToString(@"hh\:mm\:ss\.fffffff"),
            _ => v.ToString() ?? string.Empty
        };
    }
}
