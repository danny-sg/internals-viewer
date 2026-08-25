using InternalsViewer.Internals.Columnstore.Dictionaries;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

/// <summary>
/// Maps between a region of a dictionary blob and the offsets it occupies
/// </summary>
/// <remarks>
/// The two dictionary kinds lay out differently, a string dictionary carrying handles and pages where a numeric
/// one carries a flat value array, so neither ever reports a region belonging to the other.
/// </remarks>
public static class DictionaryRegions
{
    public static int GetOffset(DictionaryBlob blob, DictionaryRegion region) => blob switch
    {
        StringDictionary strings => region switch
        {
            DictionaryRegion.Handles => StringDictionary.HandleArrayOffset,
            DictionaryRegion.Pages or DictionaryRegion.Values => GetPagesOffset(strings),
            _ => 0
        },
        NumericDictionary => region == DictionaryRegion.Values ? NumericDictionary.HeaderSize : 0,
        _ => 0
    };

    public static DictionaryRegion GetRegion(DictionaryBlob blob, int offset)
    {
        switch (blob)
        {
            case StringDictionary strings:
                return offset >= GetPagesOffset(strings) ? DictionaryRegion.Pages
                     : offset >= StringDictionary.HandleArrayOffset ? DictionaryRegion.Handles
                     : DictionaryRegion.Header;

            case NumericDictionary:
                return offset >= NumericDictionary.HeaderSize ? DictionaryRegion.Values : DictionaryRegion.Header;

            default:
                return DictionaryRegion.Header;
        }
    }

    /// <summary>
    /// Where the pages start, the sizes naming them being read as part of the same region
    /// </summary>
    private static int GetPagesOffset(StringDictionary strings)
        => StringDictionary.HandleArrayOffset + (strings.HandleCount * strings.HandleSize);
}
