using System;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Loads a page from text, e.g. DBCC PAGE output
/// </summary>
public class TextPageReader(string pageText) : PageReader
{
    public string PageText { get; set; } = pageText;

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
}