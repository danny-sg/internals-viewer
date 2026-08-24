using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// Every colour the structure drawing uses, in one place so the palette can be changed without hunting for it
/// </summary>
/// <remarks>
/// Held as fields rather than constants because a colour is a struct, and paired light and dark where the drawing
/// has to read on either theme. The encoding and state colours carry meaning rather than decoration, so they hold
/// across both themes and only the surfaces around them change.
/// </remarks>
public static class ColumnstoreColours
{
    /// <summary>
    /// Builds a colour from a six digit hex value, which is how a colour is written everywhere else
    /// </summary>
    /// <remarks>
    /// Taking the whole value rather than three components means a colour can be pasted in from a picker or a style
    /// sheet as it stands. Alpha is added because these are all opaque.
    /// </remarks>
    private static SKColor FromHex(uint rgb) => new(rgb | 0xFF000000);

    // Surfaces, which change with the theme

    public static readonly SKColor Text = FromHex(0x202020);

    public static readonly SKColor DarkText = FromHex(0xECEBE6);

    public static readonly SKColor Muted = FromHex(0x707070);

    public static readonly SKColor DarkMuted = FromHex(0x9A9892);

    public static readonly SKColor Panel = FromHex(0xf5f2f2);

    public static readonly SKColor DarkPanel = FromHex(0x2A2A28);

    public static readonly SKColor Border = FromHex(0xD0CEC6);

    public static readonly SKColor DarkBorder = FromHex(0x444441);

    public static readonly SKColor Selection = FromHex(0x185FA5);

    public static readonly SKColor Hover = FromHex(0xD0CEC6);

    public static readonly SKColor DarkHover = FromHex(0x85B7EB);

    // Encodings, one colour each so the drawing reads as a map of compression techniques

    public static readonly SKColor ValueBased = FromHex(0x7F77DD);

    public static readonly SKColor ValueHashBased = FromHex(0x1D9E75);

    public static readonly SKColor StringHashBased = FromHex(0xD85A30);

    public static readonly SKColor StoreByValueBased = FromHex(0x378ADD);

    public static readonly SKColor StringStoreByValueBased = FromHex(0xD4537E);

    public static readonly SKColor UnknownEncoding = FromHex(0x888780);

    public static readonly SKColor MixedStorage = FromHex(0x2E9E8F);

    // Row group states, as a background with the text that reads on it

    public static readonly SKColor InvisibleState = FromHex(0xFFFFFF);

    public static readonly SKColor InvisibleStateText = FromHex(0x5F5E5A);

    public static readonly SKColor OpenState = FromHex(0xFAC775);

    public static readonly SKColor OpenStateText = FromHex(0x854F0B);

    public static readonly SKColor ClosedState = FromHex(0xB5D4F4);

    public static readonly SKColor ClosedStateText = FromHex(0x0C447C);

    public static readonly SKColor CompressedState = FromHex(0xC0DD97);

    public static readonly SKColor CompressedStateText = FromHex(0x3B6D11);

    public static readonly SKColor TombstoneState = FromHex(0x444441);

    public static readonly SKColor TombstoneStateText = FromHex(0xFFFFFF);

    public static readonly SKColor UnknownState = FromHex(0xD3D1C7);

    public static readonly SKColor UnknownStateText = FromHex(0x2C2C2A);

    // Dictionaries, which stay within one family being variants of the same thing

    public static readonly SKColor NumericDictionary = FromHex(0x854F0B);

    public static readonly SKColor StringDictionary = FromHex(0xEF9F27);

    public static readonly SKColor FloatDictionary = FromHex(0xBA7517);

    public static readonly SKColor UnknownDictionary = FromHex(0x888780);

    public static readonly SKColor LocatorBand = FromHex(0xE4EAF1);

    public static readonly SKColor DarkLocatorBand = FromHex(0x252C34);

    public static readonly SKColor GlobalScope = FromHex(0x2E7D6E);

    public static readonly SKColor LocalScope = FromHex(0x9C5BB8);

    public static readonly SKColor HuffmanFlag = FromHex(0xB5462F);

    public static readonly SKColor UncompressedFlag = FromHex(0x6B6A63);

    public static SKColor Shade(SKColor colour, float factor)
        => new((byte)(colour.Red * factor), (byte)(colour.Green * factor), (byte)(colour.Blue * factor));

    // Structure types, a scheme of their own so the layout is not read as an encoding

    public static readonly SKColor RunLengthStructure = FromHex(0x5E5CE6);

    public static readonly SKColor VariableLengthDataStructure = FromHex(0x0F7B6C);

    public static readonly SKColor UnknownRleType = FromHex(0x888780);

    // What a segment carries, one colour per flag

    public static readonly SKColor RleFlag = FromHex(0xD8720F);

    public static readonly SKColor BitPackFlag = FromHex(0x2C6FBB);

    public static readonly SKColor VariableLengthDataFlag = FromHex(0x8A4FBE);

    // The row sets a row group is built on

    public static readonly SKColor DeleteBitmap = FromHex(0xE24B4A);

    public static readonly SKColor DeltaStore = FromHex(0x639922);
}
