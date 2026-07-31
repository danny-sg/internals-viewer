using System;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Services.Markers;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Plan;

/// <summary>
/// Resolves a marker style for an ItemType, returning its name or fore/back brush per the converter parameter
/// </summary>
/// <remarks>
/// Reuses the same MarkStyleProvider the hex viewer's markers use, so a log record's field changes are named and coloured identically to
/// the page's markers. Parameter selects the part: "Name", "Fore" or "Back" (default).
/// </remarks>
public sealed class MarkStyleConverter : IValueConverter
{
    // Created lazily on the first (bind-time) call so the theme dictionaries are ready
    private MarkStyleProvider Provider => field ??= new MarkStyleProvider();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var part = parameter as string ?? "Fore";

        if (value is not ItemType itemType)
        {
            return part == "Name" ? string.Empty : null;
        }

        var style = Provider.GetMarkStyle(itemType);

        return part switch
        {
            "Name" => style.Name,
            "Back" => style.BackColour,
            _ => style.ForeColour
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
