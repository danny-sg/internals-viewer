using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class StepSourceToMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is int source && source > 0 ? new Thickness(12, 0, 0, 0) : new Thickness(0);
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
