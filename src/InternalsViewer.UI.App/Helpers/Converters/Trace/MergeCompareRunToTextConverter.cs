using System;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Trace;

public sealed class MergeCompareRunToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        if (value is not AccessStep.MergeCompareRun run)
        {
            return string.Empty;
        }

        return $"Outer {Range(run.OuterFrom, run.OuterTo)}, Inner {Range(run.InnerFrom, run.InnerTo)}";
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }

    private static string Range(AccessKey from, AccessKey to)
    {
        return from.Equals(to) ? from.ToString() : $"{from} - {to}";
    }
}
