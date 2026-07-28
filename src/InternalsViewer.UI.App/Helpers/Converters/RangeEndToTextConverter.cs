using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class RangeEndToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        if (value is AccessStep.RangeEnd { Comparison: 0 })
        {
            return "Exclusive range bound";
        }

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
