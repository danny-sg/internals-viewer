namespace InternalsViewer.Query.CallStack.TimeTravel.Locators;

public readonly record struct LocatorType(LocatorKind Kind, bool IsIndirect, bool IsConst, int Offset = 0)
{
    private const int AllocationPageIdOffset = 8;

    private static readonly char[] Indirections = ['*', '&'];

    public bool IsOutput => IsIndirect && !IsConst;

    public static LocatorType? Of(string type)
    {
        var indirection = type.IndexOfAny(Indirections);

        if (indirection >= 0 && type.IndexOfAny(Indirections, indirection + 1) >= 0)
        {
            return null;
        }

        var words = (indirection < 0 ? type : type[..indirection]).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var names = words.Where(w => !IsQualifier(w)).ToList();

        (LocatorKind Kind, int Offset)? found = names switch
        {
            ["PageId"] => (LocatorKind.Page, 0),
            ["AllocationPageId"] => (LocatorKind.Page, AllocationPageIdOffset),
            ["RowId"] => (LocatorKind.Row, 0),
            ["ColumnStoreObjectKey"] => (LocatorKind.ColumnStoreObject, 0),
            _ => null
        };

        return found is { } match ? new LocatorType(match.Kind, indirection >= 0, words.Contains("const"), match.Offset) : null;
    }

    private static bool IsQualifier(string word) => word is "const" or "volatile" or "class" or "struct" or "__unaligned";
}
