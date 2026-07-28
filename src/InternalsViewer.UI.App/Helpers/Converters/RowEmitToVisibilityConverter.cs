using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class RowEmitToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is AccessStep.Row { Outcome: RowOutcome.Match }
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
