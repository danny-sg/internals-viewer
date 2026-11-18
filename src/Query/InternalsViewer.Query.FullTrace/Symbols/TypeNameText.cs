namespace InternalsViewer.Query.FullTrace.Symbols;

public static class TypeNameText
{
    private const string FunctorSuffix = "Fn";

    private static readonly string[] Qualifiers = ["__ptr64", "const ", "volatile ", "class ", "struct ", "enum "];

    private static readonly string[] PolicyPrefixes = ["CFn", "Fn", "CMemObjAlloc<", "CRawMemObjAlloc<"];

    public static string Shorten(string type)
    {
        var text = Qualifiers.Aggregate(type, (current, qualifier) => current.Replace(qualifier, string.Empty)).Trim();

        var open = text.IndexOf('<');

        if (open < 0)
        {
            return Tidy(text);
        }

        var close = GetClose(text, open);

        var arguments = SplitArguments(text[(open + 1)..close]).Where(a => !IsPolicy(a)).Select(Shorten);

        return $"{Tidy(text[..open])}<{string.Join(", ", arguments)}>{Tidy(text[Math.Min(close + 1, text.Length)..])}";
    }

    internal static int GetClose(string text, int open)
    {
        var depth = 0;

        for (var index = open; index < text.Length; index++)
        {
            depth += text[index] switch
            {
                '<' => 1,
                '>' => -1,
                _ => 0
            };

            if (depth == 0)
            {
                return index;
            }
        }

        return text.Length;
    }

    internal static List<string> SplitArguments(string text)
    {
        var arguments = new List<string>();

        var depth = 0;

        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '<':
                {
                    depth++;

                    break;
                }
                case '>':
                {
                    depth--;

                    break;
                }
                case ',' when depth == 0:
                {
                    arguments.Add(text[start..index].Trim());

                    start = index + 1;

                    break;
                }
            }
        }

        arguments.Add(text[start..].Trim());

        return arguments;
    }

    private static bool IsPolicy(string argument)
        => PolicyPrefixes.Any(p => argument.StartsWith(p, StringComparison.Ordinal))
           || argument.EndsWith(FunctorSuffix, StringComparison.Ordinal);

    private static string Tidy(string text) => text.Replace(" *", "*").Replace(" &", "&").Trim();
}
