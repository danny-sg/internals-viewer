using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Locators.Parsers;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorType(LocatorValueType ValueType, bool IsIndirect, bool IsConst, int Offset = 0, int[]? Path = null)
{
    private static readonly char[] Indirections = ['*', '&'];

    public bool IsOutput => IsIndirect && !IsConst;

    public bool IsInOut => IsOutput && (ValueType == LocatorValueType.Referenced || Path is not null);

    public static LocatorType? GetLocatorType(string type)
    {
        var indirection = type.IndexOfAny(Indirections);

        if (indirection >= 0 && type.IndexOfAny(Indirections, indirection + 1) >= 0)
        {
            return null;
        }

        var target = indirection < 0 ? type.AsSpan() : type.AsSpan(0, indirection);

        var isConst = false;

        var names = 0;

        var name = ReadOnlySpan<char>.Empty;

        foreach (var range in target.Split(' '))
        {
            var word = target[range];

            if (word.IsEmpty)
            {
                continue;
            }

            isConst |= word is "const";

            if (!IsQualifier(word))
            {
                names++;

                name = word;
            }
        }

        if (names == 1
            && LocatorParsers.Default.GetKnownType(name) is { } known
            && (!known.Parser.IsConstPointerOnly || (indirection >= 0 && isConst)))
        {
            return new LocatorType(known.Parser.ValueType, indirection >= 0, isConst, known.Name.Offset, known.Name.Path);
        }

        return indirection >= 0 && ArgumentValue.IsReadable(target)
            ? new LocatorType(LocatorValueType.Referenced, true, isConst)
            : null;
    }

    private static bool IsQualifier(ReadOnlySpan<char> word) => word is "const" or "volatile" or "class" or "struct" or "__unaligned";
}
