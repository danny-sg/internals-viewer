using System.Text;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;

public class TestLogger
{
    public static ILogger<T> GetLogger<T>(ITestOutputHelper testOutput)
    {
        return new TestLogger<T>(testOutput);
    }
}

/// <summary>
/// 
/// </summary>
/// <remarks>Adapted from https://www.meziantou.net/how-to-get-asp-net-core-logs-in-the-output-of-xunit-tests.htm</remarks>
public class TestLogger<T>(ITestOutputHelper testOutput) : ILogger<T>
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    LoggerExternalScopeProvider ScopeProvider { get; set; } = new();

    public IDisposable BeginScope<TState>(TState state)
    {
        return ScopeProvider.Push(state);
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel,
                            EventId eventId,
                            TState state,
                            Exception? exception,
                            Func<TState, Exception?, string> formatter)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.Append(GetLogLevelPrefix(logLevel));
        stringBuilder.Append(formatter(state, exception));

        if (exception != null)
        {
            stringBuilder.Append('\n').Append(exception);
        }

        ScopeProvider.ForEachScope((scope, s) =>
        {
            s.Append("\n => ");
            s.Append(scope);
        }, stringBuilder);

        TestOutput.WriteLine(stringBuilder.ToString());
    }

    private static string GetLogLevelPrefix(LogLevel logLevel) => logLevel.ToString().PadRight(10)[..10];
}