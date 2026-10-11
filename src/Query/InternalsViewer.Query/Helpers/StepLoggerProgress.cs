using InternalsViewer.Internals.Engine.Loading;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.Helpers;

internal sealed class StepLoggerProgress(IProgress<ProgressDetail>? progress, ILogger logger) : IProgress<ProgressDetail>
{
    public void Report(ProgressDetail value)
    {
        progress?.Report(value);

        if (!value.IsStep || value.Percentage is not null)
        {
            return;
        }

        if (value.Elapsed is { } elapsed)
        {
            logger.LogInformation("{Step} finished in {Elapsed}", value.Message, elapsed);
        }
        else
        {
            logger.LogInformation("{Step} started", value.Message);
        }
    }
}
