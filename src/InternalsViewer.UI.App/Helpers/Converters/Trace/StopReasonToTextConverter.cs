using System;
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
            StopReason reason => reason.ToString().SplitString(),
            _ => string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
