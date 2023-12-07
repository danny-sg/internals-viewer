using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Readers.Headers;

namespace InternalsViewer.Internals.Tests.Helpers.TestReaders;

public class TestHeaderReader
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