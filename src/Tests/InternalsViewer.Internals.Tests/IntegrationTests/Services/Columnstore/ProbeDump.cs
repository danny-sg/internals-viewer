namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Where the columnstore probes leave their output
/// </summary>
public static class ProbeDump
{
    public const string Directory = @"C:\ColumnstoreDump";

    public static string Write(string fileName, IEnumerable<string> lines)
    {
        System.IO.Directory.CreateDirectory(Directory);

        var path = Path.Combine(Directory, fileName);

        File.WriteAllLines(path, lines);

        return path;
    }
}
