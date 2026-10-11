using System.Text.RegularExpressions;

namespace InternalsViewer.Query.CallStack.Arguments;

public sealed partial record FunctionSignature(IReadOnlyList<string> Parameters,
                                               FunctionKind Kind,
                                               string? ReturnType,
                                               bool HasReturnBuffer = false)
{
    public const string EnumPrefix = "enum ";

    public static FunctionSignature? Parse(string? signature, string? decoratedName)
    {
        if (signature is null || ParseParameters(signature) is not { } parameters)
        {
            return null;
        }

        if (decoratedName is not null && Decoration().Match(decoratedName) is { Success: true } match)
        {
            var encodedReturn = decoratedName[(match.Index + match.Length)..];

            return new FunctionSignature(MarkEnums(parameters, decoratedName),
                                         KindOf(match),
                                         DecodeReturnType(encodedReturn),
                                         ReturnBuffer().IsMatch(encodedReturn));
        }

        return new FunctionSignature(parameters, IsScoped(signature) ? FunctionKind.Member : FunctionKind.Free, null);
    }

    public static FunctionKind? KindOf(string decoratedName)
        => Decoration().Match(decoratedName) is { Success: true } match ? KindOf(match) : null;

    public static IReadOnlyList<string>? ParseParameters(string signature)
    {
        var open = FindParameterList(signature);

        if (open < 0)
        {
            return null;
        }

        var depth = 0;

        var close = -1;

        for (var i = open; i < signature.Length; i++)
        {
            var character = signature[i];

            if (character is '(' or '<' or '[')
            {
                depth++;
            }
            else if (character is ')' or '>' or ']')
            {
                depth--;

                if (depth == 0 && character == ')')
                {
                    close = i;

                    break;
                }
            }
        }

        if (close < 0)
        {
            return null;
        }

        var inner = signature[(open + 1)..close].Trim();

        return inner is "" or "void" ? [] : SplitTopLevel(inner);
    }

    private static List<string> MarkEnums(IReadOnlyList<string> parameters, string decoratedName)
    {
        var enums = EnumType().Matches(decoratedName)
                              .Select(m => string.Join("::", m.Groups["name"].Value.Split('@').Reverse()))
                              .ToHashSet(StringComparer.Ordinal);

        return [.. parameters.Select(p => enums.Contains(p) ? $"{EnumPrefix}{p}" : p)];
    }

    private static FunctionKind KindOf(Match match)
        => match.Groups["member"].Success ? FunctionKind.Member
           : match.Groups["static"].Success ? FunctionKind.Static
           : FunctionKind.Free;

    private static bool IsScoped(string signature)
    {
        var open = FindParameterList(signature);

        return ResolvedCallstackFrameParser.FindClassMethodSeparator(open < 0 ? signature : signature[..open]) > 0;
    }

    private static string? DecodeReturnType(string encoded)
    {
        if (encoded.Length == 0)
        {
            return null;
        }

        if (ClassType().Match(encoded) is { Success: true } named)
        {
            return DescribeClass(named);
        }

        if (encoded[0] == '_' && encoded.Length > 1)
        {
            return encoded[1] switch
            {
                'N' => "bool",
                'J' => "__int64",
                'K' => "unsigned __int64",
                'W' => "wchar_t",
                _ => null
            };
        }

        if (encoded.StartsWith("W4", StringComparison.Ordinal))
        {
            return "enum";
        }

        return encoded[0] switch
        {
            'X' => "void",
            'C' => "signed char",
            'D' => "char",
            'E' => "unsigned char",
            'F' => "short",
            'G' => "unsigned short",
            'H' => "int",
            'I' => "unsigned int",
            'J' => "long",
            'K' => "unsigned long",
            'M' => "float",
            'N' => "double",
            'O' => "long double",
            'P' or 'Q' => "void *",
            _ => null
        };
    }

    private static string DescribeClass(Match match)
    {
        var name = match.Groups["name"].Value;

        if (!match.Groups["indirection"].Success)
        {
            return name;
        }

        var qualifier = match.Groups["qualifier"].Value is "B" or "D" ? " const" : string.Empty;

        return $"{name}{qualifier} {(match.Groups["indirection"].Value == "A" ? '&' : '*')}";
    }

    private static int FindParameterList(string signature)
    {
        var depth = 0;

        for (var i = 0; i < signature.Length; i++)
        {
            var character = signature[i];

            if (character == '<')
            {
                depth++;
            }
            else if (character == '>')
            {
                depth--;
            }
            else if (character == '(' && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> SplitTopLevel(string inner)
    {
        var parameters = new List<string>();

        var depth = 0;

        var start = 0;

        for (var i = 0; i < inner.Length; i++)
        {
            var character = inner[i];

            if (character is '(' or '<' or '[')
            {
                depth++;
            }
            else if (character is ')' or '>' or ']')
            {
                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                parameters.Add(inner[start..i].Trim());

                start = i + 1;
            }
        }

        parameters.Add(inner[start..].Trim());

        return parameters;
    }

    [GeneratedRegex(@"@@(?:(?<member>[ABEFIJMNQRUV])E[ABCD]|(?<static>[CDKLST])|(?<free>[YZ]))[AQ]")]
    private static partial Regex Decoration();

    [GeneratedRegex(@"^(?:\?[ABCD]|(?<indirection>[PQA])E[FI]*(?<qualifier>[ABCD]))[UVT](?<name>[A-Za-z_]\w*)@@")]
    private static partial Regex ClassType();

    [GeneratedRegex(@"^\?[ABCD][UVT]")]
    private static partial Regex ReturnBuffer();

    [GeneratedRegex(@"W4(?<name>[A-Za-z_]\w*(?:@[A-Za-z_]\w*)*)@@")]
    private static partial Regex EnumType();
}
