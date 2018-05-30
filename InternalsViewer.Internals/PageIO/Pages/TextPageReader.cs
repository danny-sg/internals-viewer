using System;
using InternalsViewer.Internals.PageIo.Headers;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.PageIo.Pages
{
    /// <summary>
    /// Loads a page from text, e.g. DBCC PAGE output
    /// </summary>
    public class TextPageReader : PageReader
    {
        public string PageText { get; set; }

        public TextPageReader(string pageText)
        {
            PageText = pageText;
            HeaderReader = new TextHeaderReader(PageText);
        }

        public override void Load()
        {
            Data = LoadTextPage(PageText);
            LoadHeader();
        }

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
}