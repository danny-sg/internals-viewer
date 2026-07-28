using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class RowOutcomeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        if (value is not AccessStep.Row row)
        {
            return string.Empty;
        }

        return row.Outcome switch
        {
            RowOutcome.Match => row.HasResidual ? "Match" : string.Empty,
            RowOutcome.NoMatch => "No Match",
            RowOutcome.Ghost => "Ghost",
            _ => "Unknown"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
