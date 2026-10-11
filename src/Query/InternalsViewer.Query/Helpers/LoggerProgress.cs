using InternalsViewer.Internals.Engine.Loading;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.Helpers;

internal sealed class LoggerProgress(ILogger logger) : IProgress<ProgressDetail>
{
    public void Report(ProgressDetail value) => logger.LogInformation("{Progress}", value.ToString());
}
