using System;
using System.IO;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Tests.Internals.UnitTests.TestHelpers.TestReaders;

public class FilePageReader(string path) : PageReader, IPageReader
{
    private byte[] LoadTextPage(string pageText)
    {
        var offset = 0;

        var dumpFound = false;

        var data = new byte[Page.Size];

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
            if (dumpFound && !string.IsNullOrEmpty(line))
            {
                offset = ReadData(line, offset, data);
            }
        }

        return data;
    }

    public async Task<byte[]> Read(string databaseName, PageAddress pageAddress)
    {
        var pattern = $"{databaseName}_{pageAddress.FileId}_{pageAddress.PageId}_*.page";

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
