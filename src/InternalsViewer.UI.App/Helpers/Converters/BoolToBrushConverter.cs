using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters;

/// <summary>
/// Maps a boolean to one of two brushes (e.g. an "active" fill vs. transparent), set via
/// <see cref="TrueBrush"/> and <see cref="FalseBrush"/>.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }

    public Brush? FalseBrush { get; set; }

    public object? Convert(object value, Type targetType, object parameter, string language)
        => value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
