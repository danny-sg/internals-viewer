using InternalsViewer.Internals.Engine.Loading;
using System.Diagnostics;
using System.Net;

namespace InternalsViewer.Query.CallStack.Symbols;

public static class SymbolDownloader
{
    private const int BufferSize = 81_920;

    private const string PartialExtension = ".download";

    private const string NotFoundOutcome = "Not Found";

    private sealed record PdbIdentity(string Pdb, string Guid, int Age);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public static async Task DownloadSymbols(IEnumerable<CallstackFrame> frames,
                                             string symbolsDirectory,
                                             IProgress<ProgressDetail>? progress,
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

    public static string SymbolId(string guid, int age) => $"{guid.Replace("-", string.Empty)}{age:X}".ToUpperInvariant();

    private static async Task DownloadPdbAsync(PdbIdentity pdb,
                                               string symbolsDirectory,
                                               IProgress<ProgressDetail>? progress,
                                               CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pdb.Pdb))
        {
            return;
        }

        var symbolId = SymbolId(pdb.Guid, pdb.Age);

        var pdbFolder = Path.Combine(symbolsDirectory, pdb.Pdb, symbolId);

        var pdbPath = Path.Combine(pdbFolder, pdb.Pdb);

        if (File.Exists(pdbPath))
        {
            return;
        }

        Directory.CreateDirectory(pdbFolder);

        var url =
            $"https://msdl.microsoft.com/download/symbols/{pdb.Pdb}/{symbolId}/{pdb.Pdb}";

        var message = $"Downloading {pdb.Pdb}";

        var partialPath = Path.Combine(pdbFolder, $"{Guid.NewGuid():N}{PartialExtension}");

        var started = Stopwatch.GetTimestamp();

        progress?.Report(new ProgressDetail(message) { IsStep = true });

        try
        {
            using var response = await HttpClient.GetAsync(url,
                                                           HttpCompletionOption.ResponseHeadersRead,
                                                           cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                progress?.Report(new ProgressDetail(message) { IsStep = true, Outcome = NotFoundOutcome });

                return;
            }

            response.EnsureSuccessStatusCode();

            await SaveAsync(response, partialPath, message, progress, cancellationToken);

            if (File.Exists(pdbPath))
            {
                File.Delete(partialPath);
            }
            else
            {
                File.Move(partialPath, pdbPath);
            }

            progress?.Report(new ProgressDetail(message) { IsStep = true, Elapsed = Stopwatch.GetElapsedTime(started) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            File.Delete(partialPath);

            throw;
        }
        catch (Exception ex)
        {
            File.Delete(partialPath);

            progress?.Report(new ProgressDetail(message) { IsStep = true, Outcome = $"Failed: {ex.Message}" });
        }
    }

    private static async Task SaveAsync(HttpResponseMessage response,
                                        string path,
                                        string message,
                                        IProgress<ProgressDetail>? progress,
                                        CancellationToken cancellationToken)
    {
        var length = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

        await using var destination = File.Create(path);

        var buffer = new byte[BufferSize];

        long written = 0;

        var reported = -1;

        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            written += read;

            if (length is not > 0)
            {
                continue;
            }

            var percentage = (int)(written * 100 / length.Value);

            if (percentage != reported)
            {
                reported = percentage;

                progress?.Report(new ProgressDetail(message, percentage) { IsStep = true });
            }
        }
    }
}