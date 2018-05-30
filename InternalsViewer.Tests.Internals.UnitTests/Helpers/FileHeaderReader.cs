using System.IO;
using InternalsViewer.Internals.PageIo.Headers;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Tests.Internals.UnitTests.Helpers
{
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
}