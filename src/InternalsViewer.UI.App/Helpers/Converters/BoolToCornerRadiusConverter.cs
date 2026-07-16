using System;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

/// <summary>
/// Maps a boolean to one of two corner radii, set via <see cref="TrueRadius"/> and <see cref="FalseRadius"/>
/// </summary>
/// <remarks>
/// Used to square off a badge's trailing corners only when another segment follows it (e.g. a two-tone pill).
/// </remarks>
public class BoolToCornerRadiusConverter : IValueConverter
{
    public CornerRadius TrueRadius { get; set; }

    public CornerRadius FalseRadius { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? TrueRadius : FalseRadius;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
