using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression
{
    public class DictionaryEntry: Markable
    {
        public DictionaryEntry(byte[] data)
        {
            Data = data;
        }

        [MarkAttribute("", "Gray", "LemonChiffon", "PaleGoldenrod", true)]
        public byte[] Data { get; set; }
    }
}
