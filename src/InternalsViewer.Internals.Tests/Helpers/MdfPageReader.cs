using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Tests.Helpers;

internal class MdfPageReader(string filename) : PageReader, IPageReader
{
    public string Filename { get; } = filename;

    public async Task<byte[]> Read(string databaseName, PageAddress pageAddress)
    {
        var offset = pageAddress.PageId * PageData.Size;

        var data = new byte[PageData.Size];

        await using var file = File.OpenRead(filename);

        file.Seek(offset, SeekOrigin.Begin);

        _ = await file.ReadAsync(data, 0, PageData.Size);

        return data;
    }
}
