using System;
using System.Linq;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class EnumToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        var text = value?.ToString();

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return string.Concat(text.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
