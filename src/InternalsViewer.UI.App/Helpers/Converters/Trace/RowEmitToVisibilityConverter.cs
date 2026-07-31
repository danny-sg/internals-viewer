using System;
using InternalsViewer.Execution.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Trace;

public sealed class RowEmitToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is AccessStep.Row { Outcome: RowOutcome.Match } or AccessStep.RowRun { EmitCount: > 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
