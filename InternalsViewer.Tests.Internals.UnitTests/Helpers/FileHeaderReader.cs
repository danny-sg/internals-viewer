using System.IO;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Readers.Headers;

namespace InternalsViewer.Tests.Internals.UnitTests.Helpers;

public class FileHeaderReader
{
    public static Header ReadHeader(string path)
    {
        var pageString = File.ReadAllText(path);

        var reader = new TextHeaderReader(pageString);

        var header = new Header();

        reader.LoadHeader(header);

        return header;
    }
}