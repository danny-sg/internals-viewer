using InternalsViewer.Internals.Compression;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

/// <summary>
/// One symbol of a Huffman page, as the code table lists it
/// </summary>
public sealed class HuffmanCodeDetail
{
    public required HuffmanCode Code { get; init; }

    public int Symbol => Code.Symbol;

    public int BitLength => Code.BitLength;

    public string Bits => Code.Bits;

    public string HexValue => $"0x{Symbol:X2}";

    /// <summary>
    /// The character the symbol prints as, blank where it has none rather than showing a control code
    /// </summary>
    public string Character => Symbol is >= 0x20 and < 0x7F ? ((char)Symbol).ToString() : string.Empty;
}