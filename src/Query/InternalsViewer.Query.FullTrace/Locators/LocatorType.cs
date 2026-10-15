using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorType(LocatorValueType ValueType, bool IsIndirect, bool IsConst, int Offset = 0, int[]? Path = null)
{
    private const int AllocationPageIdOffset = 8;

    private const int PageHeaderPageIdOffset = 0x20;

    private const int BufferPageIdOffset = 0x70;

    public static readonly int[] PageHandlePath = [0, 0x18, 0];

    public static readonly int[] PageRefPath = [0];

    private static readonly char[] Indirections = ['*', '&'];

    public bool IsOutput => IsIndirect && !IsConst;

    public bool IsInOut => IsOutput && (ValueType == LocatorValueType.Referenced || Path is not null);

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

        var found = (names == 1 ? KnownLocator(name) : null)
                    ?? (indirection >= 0 && ArgumentValue.IsReadable(target) ? (LocatorValueType.Referenced, 0, null) : null);

        return found is { } match
            ? new LocatorType(match.ValueType, indirection >= 0, isConst, match.Offset, match.Path)
            : null;
    }

    private static (LocatorValueType ValueType, int Offset, int[]? Path)? KnownLocator(ReadOnlySpan<char> name) => name switch
    {
        "PageId" => (LocatorValueType.Page, 0, null),
        "AllocationPageId" => (LocatorValueType.Page, AllocationPageIdOffset, null),
        "HPageHandle" => (LocatorValueType.Page, PageHeaderPageIdOffset, PageHandlePath),
        "PageRef" or "BTreeHPage" or "IndexPageRef" or "HeapPageRef" or "TextPageRef" or "IAMPageRef" or "GAMPageRef"
            or "SGAMPageRef" or "PFSPageRef" or "PSPageRef" => (LocatorValueType.Page, BufferPageIdOffset, PageRefPath),
        "RowId" => (LocatorValueType.Row, 0, null),
        "ColumnStoreObjectKey" => (LocatorValueType.ColumnStoreObject, 0, null),
        "LSN" => (LocatorValueType.LogSequenceNumber, 0, null),
        "CTypeInfo" => (LocatorValueType.TypeInfo, 0, null),
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
