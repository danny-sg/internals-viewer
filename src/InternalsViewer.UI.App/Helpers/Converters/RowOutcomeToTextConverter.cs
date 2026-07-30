using System;
using InternalsViewer.Execution.AccessPaths.Results;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class RowOutcomeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        var (outcome, hasResidual, hasRange) = value switch
        {
            AccessStep.Row row => (row.Outcome, row.HasResidual, row.HasRange),
            AccessStep.RowRun run => (run.Outcome, run.HasResidual, run.HasRange),
            _ => ((RowOutcome?)null, false, false)
        };

        return outcome switch
        {
            RowOutcome.Match when hasResidual => "Predicate Match",
            RowOutcome.Match when hasRange => "In Range",
            RowOutcome.Match => "No Predicate",
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
