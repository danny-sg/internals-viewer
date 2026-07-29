using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class RowOutcomeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        var (outcome, hasResidual) = value switch
        {
            AccessStep.Row row => (row.Outcome, row.HasResidual),
            AccessStep.RowRun run => (run.Outcome, run.HasResidual),
            _ => ((RowOutcome?)null, false)
        };

        return outcome switch
        {
            RowOutcome.Match => hasResidual ? "Predicate Match" : "In Range",
            RowOutcome.NoMatch => "No Match",
            RowOutcome.Ghost => "Ghost",
            null => string.Empty,
            _ => "Unknown"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
