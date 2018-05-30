using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.PageIo.Headers
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
