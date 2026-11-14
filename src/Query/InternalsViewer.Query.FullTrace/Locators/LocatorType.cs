using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorType(LocatorValueType ValueType,
                                            bool IsIndirect,
                                            bool IsConst,
                                            LocatorPageSource PageSource = LocatorPageSource.Direct)
{
    private static readonly char[] Indirections = ['*', '&'];

    public bool IsOutput => IsIndirect && !IsConst;

    public bool IsInOut => IsOutput && (ValueType == LocatorValueType.Referenced || IsFollowed);

    private bool IsFollowed => PageSource is LocatorPageSource.PageHandle or LocatorPageSource.PageReference;

    public static LocatorType? GetLocatorType(string type)
    {
        var indirection = type.IndexOfAny(Indirections);

        if (indirection >= 0 && type.IndexOfAny(Indirections, indirection + 1) >= 0)
        {
            return null;
        }

        var target = indirection < 0 ? type.AsSpan() : type.AsSpan(0, indirection);

        var isConst = false;

        var names = 0;

        var name = ReadOnlySpan<char>.Empty;

        foreach (var range in target.Split(' '))
        {
            var word = target[range];

            if (word.IsEmpty)
            {
                continue;
            }

            isConst |= word is "const";

            if (!IsQualifier(word))
            {
                names++;

                name = word;
            }
        }

        if (indirection >= 0 && isConst && names == 1 && GetTextValueType(name) is { } textValueType)
        {
            return new LocatorType(textValueType, true, true);
        }

        var found = (names == 1 ? GetKnownLocator(name) : null)
                    ?? (indirection >= 0 && ArgumentValue.IsReadable(target)
                        ? (LocatorValueType.Referenced, LocatorPageSource.Direct)
                        : null);

        return found is { } match
            ? new LocatorType(match.ValueType, indirection >= 0, isConst, match.PageSource)
            : null;
    }

    private static (LocatorValueType ValueType, LocatorPageSource PageSource)? GetKnownLocator(ReadOnlySpan<char> name) => name switch
    {
        "PageId"
            => (LocatorValueType.Page, LocatorPageSource.Direct),
        "AllocationPageId"
            => (LocatorValueType.Page, LocatorPageSource.AllocationPageId),
        "HPageHandle"
            => (LocatorValueType.Page, LocatorPageSource.PageHandle),
        "PageRef" or "BTreeHPage" or "IndexPageRef" or "HeapPageRef" or "TextPageRef" or "IAMPageRef" or "GAMPageRef"
            or "SGAMPageRef" or "PFSPageRef" or "PSPageRef"
            => (LocatorValueType.Page, LocatorPageSource.PageReference),
        "RowId"
            => (LocatorValueType.Row, LocatorPageSource.Direct),
        "ColumnStoreObjectKey"
            => (LocatorValueType.ColumnStoreObject, LocatorPageSource.Direct),
        "LSN"
            => (LocatorValueType.LogSequenceNumber, LocatorPageSource.Direct),
        "CTypeInfo"
            => (LocatorValueType.TypeInfo, LocatorPageSource.Direct),
        "CXVariant"
            => (LocatorValueType.Variant, LocatorPageSource.Direct),
        "tagDBBINDING"
            => (LocatorValueType.Bindings, LocatorPageSource.Direct),
        _ => null
    };

    private static LocatorValueType? GetTextValueType(ReadOnlySpan<char> name) => name switch
    {
        "char" => LocatorValueType.Text,
        "wchar_t" => LocatorValueType.WideText,
        _ => null
    };

    private static bool IsQualifier(ReadOnlySpan<char> word) => word is "const" or "volatile" or "class" or "struct" or "__unaligned";
}
