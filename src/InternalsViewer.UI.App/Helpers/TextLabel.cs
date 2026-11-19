using System.Diagnostics.CodeAnalysis;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Helpers;

public static class TextLabel
{
    public const string OffsetSeparator = " +0x";

    private const char Quote = '"';

    private const string TruncatedSuffix = " (Truncated)";

    [return: NotNullIfNotNull(nameof(label))]
    public static string? Shorten(string? label) => label is null or [Quote, ..] ? label : TypeNameText.Shorten(label);

    public static string GetOffsetText(string label, long offset) => offset == 0 ? label : $"{label}{OffsetSeparator}{offset:X}";

    public static string GetFieldText(string label, string? field, long offset)
        => field is null ? GetOffsetText(label, offset) : $"{label}.{field}";

    public static StringValue? GetString(string? label)
    {
        if (label is null)
        {
            return null;
        }

        var isTruncated = label.EndsWith(TruncatedSuffix, StringComparison.Ordinal);

        var text = isTruncated ? label[..^TruncatedSuffix.Length] : label;

        return text is [Quote, .., Quote] && text.Length >= 2 ? new StringValue(text[1..^1], isTruncated) : null;
    }
}
