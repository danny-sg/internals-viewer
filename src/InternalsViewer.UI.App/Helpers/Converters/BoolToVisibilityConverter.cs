using System;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool IsInverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isTrue = value is true;

        if (IsInverse)
        {
            isTrue = !isTrue;
        }

        return isTrue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        bool isVisible = value is Visibility.Visible;
        return IsInverse ? !isVisible : isVisible;
    }
}