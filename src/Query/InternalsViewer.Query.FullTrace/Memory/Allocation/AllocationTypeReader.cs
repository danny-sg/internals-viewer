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

    private const string PointerArrayMarker = "*[";

    public static string? GetLabel(TraceCallStore calls, int replayNodeIndex, int size)
    {
        var functions = calls.GetNode(replayNodeIndex)?.GetAncestors().Select(n => n.Frame!.Function).OfType<FrameFunction>() ?? [];

        return GetLabel(functions.Take(MaximumDepth), size);
    }

    public static bool IsPointerArray(string? label) => label?.Contains(PointerArrayMarker, StringComparison.Ordinal) == true;

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
            : element.TrimStart().StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal) ? EnumSize
            : TypeNameText.GetPrimitive(name) is { Size: > 0 } primitive ? primitive.Size
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
