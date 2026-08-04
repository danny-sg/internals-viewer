using System;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Trace;

/// <summary>
/// Names the operator a step came from, the id being the one the plan gave it
/// </summary>
public sealed class NodeIdToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is int nodeId and >= 0 ? $"Node Id {nodeId}" : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
