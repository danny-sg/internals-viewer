using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Engine.Loading;

namespace InternalsViewer.Internals.Tests.Helpers;

public sealed class FilePageReader(string path) : PageReader, IPageReader
{
    public Task Initialize(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static void LoadTextPage(string pageText, byte[] buffer)
    {
        var offset = 0;

        var dumpFound = false;

        foreach (var line in pageText.Split([Environment.NewLine], StringSplitOptions.None))
        {
            if (line.StartsWith("OFFSET TABLE"))
            {
                break;
            }
            if (line.StartsWith(@"Memory Dump @"))
            {
                dumpFound = true;
                continue;
            }
            if (dumpFound && !string.IsNullOrEmpty(line) && line.Length >= 64)
            {
                offset = ReadData(line.AsSpan(20, 44), offset, buffer);
            }
        }
    }

    private async Task<string> ReadPageText(string name, PageAddress pageAddress)
    {
        var pattern = $"{name}_{pageAddress.FileId}_{pageAddress.PageId}_*.page";

        var files = Directory.GetFiles(path, pattern);

        if (files.Length == 0)
        {
            throw new FileNotFoundException($"File not found matching pattern: {pattern}");
        }

        if (files.Length > 1)
        {
            throw new ArgumentException($"Multiple files found matching pattern: {pattern}");
        }

        return await File.ReadAllTextAsync(files[0]);
    }

    public Task Initialize(CancellationToken cancellationToken, IProgress<ProgressDetail>? progress = null)
    {
        return Task.CompletedTask;
    }

    public async Task<byte[]> Read(string name, PageAddress pageAddress, CancellationToken token)
    {
        var data = new byte[PageData.Size];

        await ReadInto(name, pageAddress, data, token);

        return data;
    }

    public async Task ReadInto(string name, PageAddress pageAddress, byte[] buffer, CancellationToken token)
    {
        var pageText = await ReadPageText(name, pageAddress);

        LoadTextPage(pageText, buffer);
    }
}
