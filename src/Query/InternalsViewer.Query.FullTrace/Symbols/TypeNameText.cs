using System.Text;
using InternalsViewer.Query.FullTrace.Symbols.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols;

public static class TypeNameText
{
    internal const int MaximumStackType = 256;

    private const string FunctorSuffix = "Fn";

    private const char Separator = ' ';

    private static readonly string[] Qualifiers = ["const", "volatile", "__ptr64", "__unaligned"];

    private static readonly string[] Keywords = ["class", "struct", "enum", "union"];

    private static readonly string[] ClassKeywords = ["class", "struct"];

    private static readonly string[] PolicyPrefixes = ["CFn", "Fn", "CMemObjAlloc<", "CRawMemObjAlloc<"];

    private static readonly PrimitiveType[] PrimitiveTypes =
    [
        new("void", 0, PrimitiveTypeKind.Void),
        new("bool", 1, PrimitiveTypeKind.Bool),
        new("char", 1, PrimitiveTypeKind.SignedInteger, IsCharacter: true),
        new("signed char", 1, PrimitiveTypeKind.SignedInteger, IsCharacter: true),
        new("unsigned char", 1, PrimitiveTypeKind.UnsignedInteger, IsCharacter: true),
        new("__int8", 1, PrimitiveTypeKind.SignedInteger, IsCharacter: true),
        new("unsigned __int8", 1, PrimitiveTypeKind.UnsignedInteger, IsCharacter: true),
        new("char8_t", 1, PrimitiveTypeKind.UnsignedInteger, IsCharacter: true),
        new("wchar_t", 2, PrimitiveTypeKind.UnsignedInteger, IsCharacter: true),
        new("char16_t", 2, PrimitiveTypeKind.UnsignedInteger, IsCharacter: true),
        new("char32_t", 4, PrimitiveTypeKind.UnsignedInteger, IsCharacter: true),
        new("short", 2, PrimitiveTypeKind.SignedInteger),
        new("__int16", 2, PrimitiveTypeKind.SignedInteger),
        new("unsigned short", 2, PrimitiveTypeKind.UnsignedInteger),
        new("unsigned __int16", 2, PrimitiveTypeKind.UnsignedInteger),
        new("int", 4, PrimitiveTypeKind.SignedInteger),
        new("long", 4, PrimitiveTypeKind.SignedInteger),
        new("__int32", 4, PrimitiveTypeKind.SignedInteger),
        new("unsigned int", 4, PrimitiveTypeKind.UnsignedInteger),
        new("unsigned long", 4, PrimitiveTypeKind.UnsignedInteger),
        new("unsigned __int32", 4, PrimitiveTypeKind.UnsignedInteger),
        new("__int64", 8, PrimitiveTypeKind.SignedInteger),
        new("long long", 8, PrimitiveTypeKind.SignedInteger),
        new("unsigned __int64", 8, PrimitiveTypeKind.UnsignedInteger),
        new("unsigned long long", 8, PrimitiveTypeKind.UnsignedInteger),
        new("float", 4, PrimitiveTypeKind.Float),
        new("double", 8, PrimitiveTypeKind.Float),
        new("long double", 8, PrimitiveTypeKind.Float)
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["BYTE"] = "unsigned char",
        ["WCHAR"] = "wchar_t",
        ["ULONG"] = "unsigned long",
        ["DWORD"] = "unsigned long",
        ["LONGLONG"] = "__int64",
        ["ULONGLONG"] = "unsigned __int64"
    };

    private static readonly Dictionary<string, PrimitiveType> Primitives = CreatePrimitives();

    private static readonly Dictionary<string, PrimitiveType>.AlternateLookup<ReadOnlySpan<char>> PrimitiveLookup =
        Primitives.GetAlternateLookup<ReadOnlySpan<char>>();

    public static IEnumerable<string> Words
        => Qualifiers.Concat(Keywords).Concat(PrimitiveTypes.SelectMany(p => p.Name.Split(Separator))).Distinct();

    public static string Shorten(string type)
    {
        var text = RemoveWords(type);

        var open = text.IndexOf('<');

        if (open < 0)
        {
            return Tidy(text);
        }

        var close = GetClose(text, open);

        var arguments = SplitArguments(text[(open + 1)..close]).Where(a => !IsPolicy(a)).Select(Shorten);

        return $"{Tidy(text[..open])}<{string.Join(", ", arguments)}>{Tidy(text[Math.Min(close + 1, text.Length)..])}";
    }

    public static bool IsQualifier(ReadOnlySpan<char> word) => IsAny(Qualifiers, word);

    public static bool IsKeyword(ReadOnlySpan<char> word) => IsAny(Keywords, word);

    public static PrimitiveType? GetPrimitive(ReadOnlySpan<char> name)
        => PrimitiveLookup.TryGetValue(name, out var primitive) ? primitive : null;

    public static string Normalise(string type)
    {
        var buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return Normalise(type, buffer).ToString();
    }

    public static ReadOnlySpan<char> Normalise(ReadOnlySpan<char> type, Span<char> destination)
    {
        var length = 0;

        foreach (var range in type.Split(Separator))
        {
            var part = type[range];

            if (part.IsEmpty || IsQualifier(part))
            {
                continue;
            }

            if (length > 0)
            {
                destination[length++] = Separator;
            }

            part.CopyTo(destination[length..]);

            length += part.Length;
        }

        return destination[..length];
    }

    public static ReadOnlySpan<char> RemoveClassKeyword(ReadOnlySpan<char> type)
    {
        foreach (var keyword in ClassKeywords)
        {
            if (type.Length > keyword.Length
                && type.StartsWith(keyword, StringComparison.Ordinal)
                && type[keyword.Length] == Separator)
            {
                return type[(keyword.Length + 1)..];
            }
        }

        return type;
    }

    public static string GetBaseType(string type)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        return string.Join(Separator,
                           (indirection < 0 ? type : type[..indirection]).Split(Separator, StringSplitOptions.RemoveEmptyEntries)
                                                                          .Where(w => !IsQualifier(w) && !IsKeyword(w)));
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

    private static Dictionary<string, PrimitiveType> CreatePrimitives()
    {
        var primitives = PrimitiveTypes.ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var (alias, name) in Aliases)
        {
            primitives[alias] = primitives[name];
        }

        return primitives;
    }

    private static bool IsAny(string[] words, ReadOnlySpan<char> word)
    {
        foreach (var candidate in words)
        {
            if (word.SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static string RemoveWords(string type)
    {
        var text = new StringBuilder(type.Length);

        var index = 0;

        while (index < type.Length)
        {
            if (!IsIdentifier(type[index]))
            {
                text.Append(type[index++]);

                continue;
            }

            var end = index;

            while (end < type.Length && IsIdentifier(type[end]))
            {
                end++;
            }

            var word = type.AsSpan(index, end - index);

            if (IsQualifier(word) || IsKeyword(word))
            {
                while (end < type.Length && type[end] == Separator)
                {
                    end++;
                }
            }
            else
            {
                text.Append(word);
            }

            index = end;
        }

        return text.ToString().Trim();
    }

    private static bool IsIdentifier(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';

    private static bool IsPolicy(string argument)
        => PolicyPrefixes.Any(p => argument.StartsWith(p, StringComparison.Ordinal))
           || argument.EndsWith(FunctorSuffix, StringComparison.Ordinal);

    private static string Tidy(string text) => text.Replace(" *", "*").Replace(" &", "&").Trim();
}
