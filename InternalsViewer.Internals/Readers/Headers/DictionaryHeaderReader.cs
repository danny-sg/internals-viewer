using System.Collections.Generic;

namespace InternalsViewer.Internals.Readers.Headers;

/// <summary>
/// Readers header information from a given key value pair dictionary
/// </summary>
public class DictionaryHeaderReader(IDictionary<string, string> headerData) : HeaderReader
{
    private IDictionary<string, string> HeaderData { get; } = headerData;

    protected override string GetValue(string key)
    {
        return HeaderData[key];
    }
}