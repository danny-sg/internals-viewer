using System.Collections.Generic;

namespace InternalsViewer.Internals.Readers.Headers
{
    /// <summary>
    /// Readers header information from a given key value pair dictionary
    /// </summary>
    public class DictionaryHeaderReader: HeaderReader
    {
        public DictionaryHeaderReader(IDictionary<string, string> headerData)
        {
            HeaderData = headerData;
        }

        private IDictionary<string, string> HeaderData { get; }

        protected override string GetValue(string key)
        {
            return HeaderData[key];
        }
    }
}
