using InternalsViewer.Query.Callstack;
using System.Net;

namespace InternalsViewer.Query.CallStack.Symbols;

public static class SymbolDownloader
{
    private sealed record PdbIdentity(string Pdb, string Guid, int Age);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public static async Task DownloadSymbols(IEnumerable<CallstackFrame> frames,
                                             string symbolsDirectory,
                                             IProgress<string>? progress,
                                             CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(symbolsDirectory);

        var pdbs = frames.Select(f => new PdbIdentity(f.Pdb, f.Guid, f.Age))
                         .Distinct()
                         .ToList();

        await Parallel.ForEachAsync(pdbs,
                                    cancellationToken,
                                    async (pdb, ct) => await DownloadPdbAsync(pdb, symbolsDirectory, progress, ct));
    }

    private static async Task DownloadPdbAsync(PdbIdentity pdb,
                                               string symbolsDirectory,
                                               IProgress<string>? progress,
                                               CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pdb.Pdb))
        {
            return;
        }

        var symbolId = pdb.Guid.Replace("-", string.Empty) + pdb.Age;

        var pdbFolder = Path.Combine(symbolsDirectory,
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

        progress?.Report($"Downloading {url} to {pdbPath}");

        try
        {
            using var response = await HttpClient.GetAsync(url,
                                                           HttpCompletionOption.ResponseHeadersRead,
                                                           cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                progress?.Report($"{url}: Not found");
                return;
            }

            response.EnsureSuccessStatusCode();

            var bytes = response.Content.Headers.ContentLength;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

            await using var destination = File.Create(pdbPath);

            await source.CopyToAsync(destination, cancellationToken);

            progress?.Report($"{pdb.Pdb}: Downloaded - {bytes / 1024} KB");
        }
        catch (Exception ex)
        {
            progress?.Report($"{pdb.Pdb}: Failed: {ex.Message}");
        }
    }
}