using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.UI.App.Helpers.Converters;

/// <summary>Converts an <see cref="OperatorCategory"/> to the brush the plan views tint that category with</summary>
public sealed class OperatorCategoryToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        // The same category palette EventColourProvider.GetOperatorCategoryColour uses, kept in sync by eye.
        var color = value is OperatorCategory category
            ? category switch
            {
                OperatorCategory.DataAccess => Color.FromArgb(255, 97, 176, 227),
                OperatorCategory.Join => Color.FromArgb(255, 96, 200, 120),
                OperatorCategory.Transformation => Color.FromArgb(255, 232, 150, 70),
                OperatorCategory.Buffer => Color.FromArgb(255, 170, 120, 220),
                OperatorCategory.Modification => Color.FromArgb(255, 214, 48, 49),
                _ => Colors.Gray,
            }
            : Colors.Gray;

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
