using System.Text;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Dictionary holding string values in one or more pages addressed by a handle array
/// </summary>
public sealed class StringDictionary : DictionaryBlob
{
    public const int HandleArrayOffset = 0x50;

    public int MaxStringSize { get; set; }

    public int LastStringIndex { get; set; }

    public int HandleSize { get; set; }

    public StringHandle[] Handles { get; set; } = [];

    public int[] PageSizes { get; set; } = [];

    public StringPage[] Pages { get; set; } = [];

    public Encoding Encoding { get; set; } = Encoding.Latin1;

    public string GetValue(long dataId) => GetValueAt(GetIndex(dataId));

    public string GetValueAt(int index)
    {
        var handle = Handles[index];

        return Pages[handle.Page].GetValue(handle.Offset, Encoding);
    }
}
