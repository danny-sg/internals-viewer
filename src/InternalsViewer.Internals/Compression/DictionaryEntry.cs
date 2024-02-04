using System.Text;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Compression;

public class DictionaryEntry(int symbol, ushort offset, byte[] data) : DataStructure
{
    [DataStructureItem(ItemType.DictionarySymbol)]
    public int Symbol { get; } = symbol;

    [DataStructureItem(ItemType.DictionaryValue)]
    public byte[] Data { get; } = data;

    public ushort Offset { get; } = offset;

    public override string ToString()
    {
        return $"Dictionary Entry = Symbol {Symbol}, offset {Offset} length {Data.Length}";
    }
}