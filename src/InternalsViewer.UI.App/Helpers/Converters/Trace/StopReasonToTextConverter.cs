using System;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Trace;

public sealed class StopReasonToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            StopReason.PageExhausted
                or StopReason.IndexExhausted
                or StopReason.AllocationExhausted
                or StopReason.RowGroupsExhausted => "Exhausted",
            StopReason reason => Spaced(reason.ToString()),
            _ => string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }

    private static string Spaced(string text)
        => string.Concat(text.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
}
