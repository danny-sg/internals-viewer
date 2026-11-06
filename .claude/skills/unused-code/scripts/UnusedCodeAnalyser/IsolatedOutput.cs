using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UnusedCodeAnalyser;

internal static partial class IsolatedOutput
{
    public const string OutDir = @"bin\analysis\";

    public const string IntermediateOutputPath = @"obj\analysis\";

    private const string FolderName = "analysis";

    public static HashSet<string> Find(string solutionDirectory)
        => [.. Directory.EnumerateDirectories(solutionDirectory, FolderName, SearchOption.AllDirectories)
                        .Where(d => Path.GetFileName(Path.GetDirectoryName(d)) is "bin" or "obj")];

    public static bool BuildAnalyzers(string solutionDirectory)
    {
        var analyzers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in Directory.EnumerateFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(project);

            foreach (Match reference in ProjectReference().Matches(text))
            {
                if (!reference.Value.Contains("OutputItemType=\"Analyzer\"", StringComparison.OrdinalIgnoreCase)
                    || IncludeAttribute().Match(reference.Value) is not { Success: true } include)
                {
                    continue;
                }

                analyzers.Add(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include.Groups[1].Value)));
            }
        }

        foreach (var analyzer in analyzers)
        {
            Console.WriteLine($"Building {Path.GetFileNameWithoutExtension(analyzer)}");

            using var process = Process.Start(new ProcessStartInfo("dotnet",
                                                                   ["build",
                                                                    analyzer,
                                                                    "-c",
                                                                    "Release",
                                                                    $"-p:OutDir={OutDir}",
                                                                    $"-p:IntermediateOutputPath={IntermediateOutputPath}",
                                                                    "-v",
                                                                    "q",
                                                                    "-nologo"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine(output);

                return false;
            }
        }

        return true;
    }

    public static void Remove(string solutionDirectory, HashSet<string> existing)
    {
        foreach (var directory in Find(solutionDirectory).Where(d => !existing.Contains(d)))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Could not remove {directory}: {exception.Message}");
            }
        }
    }

    [GeneratedRegex(@"<ProjectReference\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectReference();

    [GeneratedRegex("Include=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex IncludeAttribute();
}
