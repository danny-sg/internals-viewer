using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class ProbeStartToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        if (value is not AccessStep.ProbeStart probeStart)
        {
            return string.Empty;
        }

        if (probeStart.Rule is null)
        {
            return probeStart.Direction == ScanDirection.Forward
                ? "No lower bound — start of page"
                : "No upper bound — end of page";
        }

        if (!probeStart.IsLeaf)
        {
            return "Child page selection";
        }

        return probeStart.Direction == ScanDirection.Forward
            ? "Start of forward read"
            : "Start of backward read";
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
