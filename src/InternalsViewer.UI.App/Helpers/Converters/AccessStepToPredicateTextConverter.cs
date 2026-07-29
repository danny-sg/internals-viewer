using System;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class AccessStepToPredicateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            AccessStep.Probe probe => PredicateText.From(probe),
            AccessStep.ProbeStart probeStart => PredicateText.From(probeStart),
            AccessStep.ProbeResult probeResult => PredicateText.From(probeResult),
            AccessStep.RangeEnd rangeEnd => PredicateText.From(rangeEnd),
            AccessStep.Reseek reseek => PredicateText.From(reseek.Bounds),
            _ => PredicateText.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
