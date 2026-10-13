using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorType(LocatorKind Kind, bool IsIndirect, bool IsConst, int Offset = 0, int[]? Path = null)
{
    private const int AllocationPageIdOffset = 8;

    private const int PageHeaderPageIdOffset = 0x20;

    public static readonly int[] PageHandlePath = [0, 0x18, 0];

    private static readonly char[] Indirections = ['*', '&'];

    public bool IsOutput => IsIndirect && !IsConst;

    public bool IsInOut => IsOutput && (Kind == LocatorKind.Referenced || Path is not null);

    public static LocatorType? LocatorTypeOf(string type)
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

        var found = (names == 1 ? KnownLocator(name) : null)
                    ?? (indirection >= 0 && ArgumentValue.IsReadable(target) ? (LocatorKind.Referenced, 0, null) : null);

        return found is { } match
            ? new LocatorType(match.Kind, indirection >= 0, isConst, match.Offset, match.Path)
            : null;
    }

    private static (LocatorKind Kind, int Offset, int[]? Path)? KnownLocator(ReadOnlySpan<char> name) => name switch
    {
        "PageId" => (LocatorKind.Page, 0, null),
        "AllocationPageId" => (LocatorKind.Page, AllocationPageIdOffset, null),
        "HPageHandle" => (LocatorKind.Page, PageHeaderPageIdOffset, PageHandlePath),
        "RowId" => (LocatorKind.Row, 0, null),
        "ColumnStoreObjectKey" => (LocatorKind.ColumnStoreObject, 0, null),
        _ => null
    };

    private static bool IsQualifier(ReadOnlySpan<char> word) => word is "const" or "volatile" or "class" or "struct" or "__unaligned";
}
