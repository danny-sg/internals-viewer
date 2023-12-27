using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Tests.Helpers;

public class FilePageReader(string path) : PageReader, IPageReader
{
    private byte[] LoadTextPage(string pageText)
    {
        var offset = 0;

        var dumpFound = false;

        var data = new byte[PageData.Size];

        foreach (var line in pageText.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
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
                offset = ReadData(line, offset, data);
            }
        }

        return data;
    }

    public async Task<byte[]> Read(string name, PageAddress pageAddress)
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

        var pageText = await File.ReadAllTextAsync(files[0]);

        return LoadTextPage(pageText);
    }
}
