using System;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Helpers;

public static class TextLabel
{
    private const char Quote = '"';

    private const string TruncatedSuffix = " (Truncated)";

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
