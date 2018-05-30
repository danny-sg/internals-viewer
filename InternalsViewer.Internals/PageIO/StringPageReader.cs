using System;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.PageIo
{
    public class StringPageReader : PageReader
    {
        public string PageText { get; set; }

        public StringPageReader(string pageText)
        {
            PageText = pageText;
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

        public override bool LoadHeader()
        {
            return false;
            //throw new NotImplementedException();
        }
    }
}