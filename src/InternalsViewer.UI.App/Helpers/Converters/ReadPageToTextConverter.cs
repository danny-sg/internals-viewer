using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class ReadPageToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        if (value is not AccessStep.ReadPage readPage)
        {
            return string.Empty;
        }

        return (readPage.IsRoot, readPage.IsLeaf) switch
        {
            (true, true) => "(Root, Leaf)",
            (true, false) => "(Root)",
            (false, true) => "(Leaf)",
            _ => "(Intermediate)"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
