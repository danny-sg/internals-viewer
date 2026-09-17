using System;
using Windows.UI;
using InternalsViewer.Query.Plans.Operators;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters.CallStack;

/// <summary>Converts an <see cref="OperatorCategory"/> to the brush the plan views tint that category with</summary>
public sealed class OperatorCategoryToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
        => new SolidColorBrush(value is OperatorCategory category ? ColourFor(category) : Colors.Gray);

    public static Color ColourFor(OperatorCategory category)
        => category switch
        {
            OperatorCategory.DataAccess => Color.FromArgb(255, 97, 176, 227),
            OperatorCategory.Join => Color.FromArgb(255, 96, 200, 120),
            OperatorCategory.Transformation => Color.FromArgb(255, 232, 150, 70),
            OperatorCategory.Buffer => Color.FromArgb(255, 170, 120, 220),
            OperatorCategory.Modification => Color.FromArgb(255, 214, 48, 49),
            _ => Colors.Gray,
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
