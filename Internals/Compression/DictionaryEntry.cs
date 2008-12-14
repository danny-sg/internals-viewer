using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression
{
    public class DictionaryEntry: Markable
    {
        private byte[] data;

        public DictionaryEntry(byte[] data)
        {
            this.Data = data;
        }

        [MarkAttribute("", "Gray", "LemonChiffon", "PaleGoldenrod", true)]
        public byte[] Data
        {
            get { return data; }
            set { data = value; }
        }
    }
}
