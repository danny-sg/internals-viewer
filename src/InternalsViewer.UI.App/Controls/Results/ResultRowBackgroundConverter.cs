using System;
using Windows.UI;
using InternalsViewer.Query.Results;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Results;

internal sealed class ResultRowBackgroundConverter : IValueConverter
{
    internal static readonly SolidColorBrush NullBrush =
        new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xE5));

    internal static readonly SolidColorBrush TransparentBrush =
        new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ResultRow<long> row && parameter is int ordinal)
        {
            return row[ordinal] is null ? NullBrush : TransparentBrush;
        }

        return TransparentBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}