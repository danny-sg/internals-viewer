using System.Collections.Generic;
using System.Linq;

namespace InternalsViewer.UI.App.Helpers;

public static class PointerArrayText
{
    public const int MaximumShown = 4;

    public const string PointerName = "Pointer";

    private const string ArraySuffix = "[]";

    public static string GetLabel(IReadOnlyList<string> names, bool isArray)
        => names.Distinct().Count() == 1
            ? $"{PointerName}<{names[0]}>{(isArray ? ArraySuffix : string.Empty)}"
            : $"{PointerName}{Get(names)}";

    public static string Get(IReadOnlyList<string> names)
        => names.Count switch
        {
            1 => names[0],
            > MaximumShown => $"[{string.Join(", ", names.Take(MaximumShown))}, {GetMoreText(names.Count)}]",
            _ => $"[{string.Join(", ", names)}]"
        };

    public static string GetMoreText(int count) => $"+{count - MaximumShown} More";
}
