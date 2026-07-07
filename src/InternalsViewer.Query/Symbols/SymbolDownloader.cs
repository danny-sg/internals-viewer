using InternalsViewer.Query.Events.EventTypes;
using System.Net;

namespace InternalsViewer.Query.Symbols;

public static class SymbolDownloader
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public static async Task DownloadSymbols(IEnumerable<CallStackFrame> frames,
                                             string symbolCacheDirectory,
                                             CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(symbolCacheDirectory);

        var pdbs = frames.Select(f => new PdbIdentity(f.Pdb, f.Guid, f.Age))
                         .Distinct()
                         .ToList();

        await Parallel.ForEachAsync(pdbs,
                                    cancellationToken,
                                    async (pdb, ct) => await DownloadPdbAsync(pdb, symbolCacheDirectory, ct));
    }

    private static async Task DownloadPdbAsync(PdbIdentity pdb,
                                               string symbolCacheDirectory,
                                               CancellationToken cancellationToken)
    {
        var symbolId = pdb.Guid.Replace("-", string.Empty) + pdb.Age;

        var pdbFolder = Path.Combine(symbolCacheDirectory,
                                     pdb.Pdb,
                                     symbolId.ToUpperInvariant());

        var pdbPath = Path.Combine(pdbFolder, pdb.Pdb);

        if (File.Exists(pdbPath))
        {
            return;
        }

        Directory.CreateDirectory(pdbFolder);

        var url =
            $"https://msdl.microsoft.com/download/symbols/{pdb.Pdb}/{symbolId.ToUpperInvariant()}/{pdb.Pdb}";

        try
        {
            Console.WriteLine($"Downloading {pdb.Pdb}");

            using var response = await HttpClient.GetAsync(url,
                                                           HttpCompletionOption.ResponseHeadersRead,
                                                           cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Not found: {pdb.Pdb}");
                return;
            }

            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

            await using var destination = File.Create(pdbPath);

            await source.CopyToAsync(destination, cancellationToken);

            Console.WriteLine($"Downloaded: {pdbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {pdb.Pdb} : {ex.Message}");
        }
    }
}