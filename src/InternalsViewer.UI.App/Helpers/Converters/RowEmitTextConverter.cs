using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class RowEmitTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            AccessStep.RowRun run => $"→ Emit {run.EmitCount:N0}",
            _ => "→ Emit"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
