using System.Collections.Generic;
using System.Linq;

namespace InternalsViewer.UI.App.Helpers;

public static class PointerArrayText
{
    public const int MaximumShown = 4;

    public static string Get(IReadOnlyList<string> names)
        => names.Count switch
        {
            1 => names[0],
            > MaximumShown => $"[{string.Join(", ", names.Take(MaximumShown))}, {GetMoreText(names.Count)}]",
            _ => $"[{string.Join(", ", names)}]"
        };

    public static string GetMoreText(int count) => $"+{count - MaximumShown} More";
}
