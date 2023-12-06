using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression;

public class DictionaryEntry(byte[] data) : Markable
{
    [Mark(MarkType.Value)]
    public byte[] Data { get; set; } = data;
}