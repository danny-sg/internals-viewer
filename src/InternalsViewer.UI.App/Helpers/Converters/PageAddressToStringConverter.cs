using System;
using InternalsViewer.Internals.Engine.Parsers;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public class PageAddressToStringConverter: IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, string language)
    {

        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        if (value?.ToString() is null)
        {
            return null;
        }

        var stringValue = value.ToString();

        if (PageAddressParser.TryParse(stringValue!, out var pageAddress))
        {
            return pageAddress;
        }

        return null;
    }
}