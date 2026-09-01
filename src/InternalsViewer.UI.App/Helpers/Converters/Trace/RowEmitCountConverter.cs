using System;
using System.Globalization;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Trace;

public sealed class RowEmitCountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
        => value is AccessStep.RowRun run ? run.EmitCount.ToString("N0", CultureInfo.CurrentCulture) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object parameter, string language) => null;
}
