using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

public static class AllocationTypeReader
{
    private const int MaximumDepth = 5;

    private const int PointerSize = sizeof(ulong);

    private const int EnumSize = sizeof(int);

    private const string ArrayMethod = "AllocArray<";

    private const string ObjectMethod = "Alloc<";

    private const string DynamicArrayClass = "CTDynArray<";

    private const string EnumPrefix = "enum ";

    private static readonly Dictionary<string, int> ElementSizes = new()
    {
        ["bool"] = 1,
        ["char"] = 1,
        ["unsigned char"] = 1,
        ["BYTE"] = 1,
        ["short"] = 2,
        ["unsigned short"] = 2,
        ["wchar_t"] = 2,
        ["WCHAR"] = 2,
        ["int"] = 4,
        ["unsigned int"] = 4,
        ["long"] = 4,
        ["unsigned long"] = 4,
        ["float"] = 4,
        ["ULONG"] = 4,
        ["DWORD"] = 4,
        ["__int64"] = 8,
        ["unsigned __int64"] = 8,
        ["double"] = 8,
        ["LONGLONG"] = 8,
        ["ULONGLONG"] = 8
    };

    public static string? GetLabel(TraceCallStore calls, int callNodeIndex, int size)
    {
        var functions = new List<FrameFunction>();

        for (var node = calls.GetNode(callNodeIndex); node is not null && functions.Count < MaximumDepth; node = node.Parent)
        {
            if (node.Frame?.Function is { } function)
            {
                functions.Add(function);
            }
        }

        return GetLabel(functions, size);
    }

    internal static string? GetLabel(IEnumerable<FrameFunction> functions, int size)
    {
        foreach (var function in functions)
        {
            if (function.MethodName.StartsWith(ArrayMethod, StringComparison.Ordinal))
            {
                return GetArrayLabel(GetFirstArgument(function.MethodName), size);
            }

            if (function.ClassName?.StartsWith(DynamicArrayClass, StringComparison.Ordinal) == true)
            {
                return GetArrayLabel(GetFirstArgument(function.ClassName), size);
            }

            if (function.MethodName.StartsWith(ObjectMethod, StringComparison.Ordinal))
            {
                return TypeNameText.Shorten(GetFirstArgument(function.MethodName));
            }
        }

        return null;
    }

    private static string GetArrayLabel(string element, int size)
    {
        var name = TypeNameText.Shorten(element);

        int? elementSize = element.Contains('*') ? PointerSize
            : element.TrimStart().StartsWith(EnumPrefix, StringComparison.Ordinal) ? EnumSize
            : ElementSizes.TryGetValue(name, out var found) ? found
            : null;

        return elementSize is { } known && size % known == 0 ? $"{name}[{size / known}]" : $"{name}[]";
    }

    private static string GetFirstArgument(string name)
    {
        var open = name.IndexOf('<');

        return open < 0
            ? name
            : TypeNameText.SplitArguments(name[(open + 1)..TypeNameText.GetClose(name, open)]).FirstOrDefault() ?? string.Empty;
    }
}
