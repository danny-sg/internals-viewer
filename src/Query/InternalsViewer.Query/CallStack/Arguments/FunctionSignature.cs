using System.Text.RegularExpressions;

namespace InternalsViewer.Query.CallStack.Arguments;

public sealed partial record FunctionSignature(IReadOnlyList<string> Parameters, FunctionKind Kind, string? ReturnType)
{
    public static FunctionSignature? Parse(string? signature, string? decoratedName)
    {
        if (signature is null || ParseParameters(signature) is not { } parameters)
        {
            return null;
        }

        if (decoratedName is not null && Decoration().Match(decoratedName) is { Success: true } match)
        {
            return new FunctionSignature(parameters,
                                         KindOf(match),
                                         DecodeReturnType(decoratedName[(match.Index + match.Length)..]));
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
}
