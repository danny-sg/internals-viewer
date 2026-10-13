using System.Globalization;

namespace InternalsViewer.Query.Helpers;

public static class SymbolsHelper
{
    /// <summary>
    /// Finds the last "::" that separates the class from the method, ignoring any "::" nested inside template/lambda angle brackets
    /// </summary>
    public static int FindClassMethodSeparator(ReadOnlySpan<char> symbolPart)
    {
        var depth = 0;
        var lastSeparator = -1;

        for (var i = 0; i < symbolPart.Length; i++)
        {
            switch (symbolPart[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    if (depth > 0)
                    {
                        depth--;
                    }
                    break;
                case ':' when depth == 0 && i + 1 < symbolPart.Length && symbolPart[i + 1] == ':':
                    lastSeparator = i;
                    i++;
                    break;
            }
        }

        return lastSeparator;
    }

    public static void Split(ReadOnlySpan<char> symbol, out ReadOnlySpan<char> className, out ReadOnlySpan<char> methodName)
    {
        var separator = FindClassMethodSeparator(symbol);

        className = separator >= 0 ? symbol[..separator] : default;

        methodName = separator >= 0 ? symbol[(separator + 2)..] : symbol;
    }

    public static void SplitOffset(string symbol, out string name, out uint? offset)
    {
        var plusIndex = symbol.LastIndexOf('+');

        name = plusIndex >= 0 ? symbol[..plusIndex] : symbol;

        offset = plusIndex >= 0 ? ParseOffset(symbol.AsSpan(plusIndex + 1)) : null;
    }

    private static uint? ParseOffset(ReadOnlySpan<char> text)
    {
        text = text.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset) ? offset : null;
    }
}
