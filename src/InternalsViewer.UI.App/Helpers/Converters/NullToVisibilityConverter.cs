using System;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public bool IsInverse { get; set; }

    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        bool isNull = value is null;

        if (IsInverse)
        {
            isNull = !isNull;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
