using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Tests.Helpers.TestReaders;

public class TestPageReader
{
    public static byte[] ReadPage(string path)
    {
        var pageString = File.ReadAllText(path);

        var reader = new TextPageReader(pageString);

        reader.Load();

        return reader.Data;
    }
}