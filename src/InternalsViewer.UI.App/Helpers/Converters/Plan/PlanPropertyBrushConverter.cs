using System;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Plan;

public sealed class PlanPropertyBrushConverter : IValueConverter
{
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush True = new(Microsoft.UI.Colors.DarkGreen);

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush False = new(Microsoft.UI.Colors.Maroon);

    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        switch (value)
        {
            case PlanNodeProperty { IsValueSuccess: true }:
                return True;

            case PlanNodeProperty { IsValueError: true }:
                return False;
        }

        var key = value is PlanNodeProperty { IsValueHighlighted: true }
            ? "SystemFillColorCautionBrush"
            : "TextFillColorPrimaryBrush";

        return Application.Current.Resources[key];
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
