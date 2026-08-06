using System;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Trace;

public sealed class RowEmitTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            AccessStep.RowRun run => $"→ Emit {run.EmitCount:N0} rows",
            _ => "→ Emit row"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
