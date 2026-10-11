using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Engine.Loading;

public sealed class SubStepProgress(StepProgress steps, ILogger logger) : IProgress<ProgressDetail>
{
    private string LastMessage { get; set; } = string.Empty;

    public void Report(ProgressDetail value)
    {
        if (value.Message != LastMessage)
        {
            LastMessage = value.Message;

            logger.LogInformation("{Detail}", value.Message);
        }

        if (value.Percentage is { } percentage)
        {
            steps.Report(percentage);
        }
    }
}
