using InternalsViewer.Internals.Columnstore.Dictionaries;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One step of a Huffman decode as the step table lists it
/// </summary>
public sealed class DecodeStepDetail
{
    public required HuffmanDecodeStep Step { get; init; }

    public required int Ordinal { get; init; }

    public int BitOffset => Step.BitOffset;

    public int BitLength => Step.BitLength;

    public string Bits => Step.Bits;

    public string HexValue => $"0x{Step.Symbol:X2}";

    public string Character => Step.Character;

    /// <summary>
    /// What the symbol was read for, the first step or two carrying the entry length rather than its content
    /// </summary>
    public string Role => Step.IsLength ? "Length" : "Value";
}
