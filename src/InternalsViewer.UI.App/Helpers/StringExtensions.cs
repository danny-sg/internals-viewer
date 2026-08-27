using System.Linq;

namespace InternalsViewer.UI.App.Helpers;

public static class StringExtensions
{
    public static string SplitString(this string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
}
