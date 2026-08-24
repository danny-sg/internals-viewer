using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

/// <summary>
/// One string page of a dictionary as the viewer presents it
/// </summary>
public sealed class DictionaryPageSummary
{
    public required int Index { get; init; }

    public required StringPage Page { get; init; }

    public string Coding => Page is HuffmanStringPage ? "Huffman" : "Uncompressed";

    public string SubLobTypeDescription => Page.SubLobType.ToString().SplitCamelCase();

    public int StringCount => Page.StringCount;

    public int Offset => Page.Offset;

    public int Size => Page.Size;

    public string OffsetDescription => $"0x{Offset:X}";

    public HuffmanStringPage? Huffman => Page as HuffmanStringPage;

    /// <summary>
    /// Symbols the page codes, being characters on a narrow page and raw bytes on a byte page
    /// </summary>
    public string SymbolDescription => Huffman is not { } huffman
        ? string.Empty
        : huffman.HuffmanBlobType == HuffmanStringPage.NarrowBlobType ? "Characters" : "Bytes";

    public string CompressedSizeDescription => Huffman is { } huffman ? $"{huffman.CompressedDataSize}" : string.Empty;

    public IReadOnlyList<HuffmanCode> Codes => field ??= Huffman?.GetCodes() ?? [];

    public HuffmanTreeNode? Tree => Huffman is null ? null : field ??= HuffmanTreeNode.Build(Codes);
}