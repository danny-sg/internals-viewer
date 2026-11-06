using System.Diagnostics;
using System.Reflection;
using UnusedCodeAnalyser;

const string AnalyseOption = "--analyse";

var isChild = args.Length > 0 && args[0] == AnalyseOption;

var arguments = isChild ? args[1..] : args;

if (arguments.Length < 3)
{
    Console.Error.WriteLine("Usage: UnusedCodeAnalyser <solution> <output directory> <assembly> [<assembly> ...]");

    return 1;
}

var solutionPath = Path.GetFullPath(arguments[0]);

var outputDirectory = Path.GetFullPath(arguments[1]);

var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

if (isChild)
{
    return await Analysis.RunAsync(solutionPath, solutionDirectory, outputDirectory, arguments.Skip(2).ToHashSet(StringComparer.Ordinal));
}

var existing = IsolatedOutput.Find(solutionDirectory);

try
{
    if (!IsolatedOutput.BuildAnalyzers(solutionDirectory))
    {
        return 1;
    }

    var processPath = Environment.ProcessPath!;

    var start = new ProcessStartInfo(processPath);

    if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
    {
        start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
    }

    start.ArgumentList.Add(AnalyseOption);

    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using var analysis = Process.Start(start)!;

    await analysis.WaitForExitAsync();

    return analysis.ExitCode;
}
finally
{
    IsolatedOutput.Remove(solutionDirectory, existing);
}
