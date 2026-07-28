using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class SeekRuleToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            SeekRule.LowestGreaterOrEqual or SeekRule.LowestGreater => "Lowest",
            SeekRule.HighestLessOrEqual or SeekRule.HighestLess => "Highest",
            _ => string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
