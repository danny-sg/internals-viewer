using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.CallStack.Arguments;

/// <summary>
/// Parsed C++ function signature giving the parameter types, function type and return type
/// </summary>
/// <remarks>
/// Parsing combines two sources from the PDB symbols:
///
///     - The undecorated signature text, e.g. name(type, type) - provides the parameter list, split on top level commas so template
///       arguments and nested parentheses are kept intact
/// 
///     - The MSVC decorated (mangled) name - provides information the signature text does not include.
///
/// Decorated/mangled names have additional information about the functions encoded in their name. The decorated name is parsed and used to
/// determine:
///
///     - Function type - the code after the @@ terminator identifies member, static member or free functions (function that is not a member
///       of any class)
/// 
///     - Return type - decoded from the mangled return type code (primitives, pointers/references to classes, enums)
///
///     - HasReturnBuffer - set when a class/struct/union is returned by value, meaning the caller passes a hidden
///       return buffer pointer as an argument
///
///     - Enum parameters - parameters whose type is an enum are prefixed with "enum " so later layout/locator logic can
///       treat them as integers rather than classes
///
/// If no function code is found in the decorated name the function type falls back to scoping in the signature text (Class::Method implies
/// member) and the return type is unknown (Class::Method implies a member, otherwise free).
///
/// Parsing is buffer/ReadOnlySpan based so scanning, filtering and the comparison helpers (CountParameters, HasSameParameters,
/// HashParameters) do not allocate. Strings are only allocated when a signature is successfully parsed into a result.
/// </remarks>
public sealed record FunctionSignature(IReadOnlyList<string> Parameters,
                                       FunctionType Type,
                                       string? ReturnType,
                                       bool HasReturnBuffer = false)
{
    public const string EnumPrefix = "enum ";

    private const string EnumCode = "W4";

    private const string NameEnd = "@@";

    private const string MemberAccessCodes = "ABEFIJMNQRUV";

    private const string StaticAccessCodes = "CDKLST";

    public static FunctionSignature? Parse(ReadOnlySpan<char> signature, ReadOnlySpan<char> decoratedName)
    {
        if (ParseParameters(signature) is not { } parameters)
        {
            return null;
        }

        if (FindFunctionCode(decoratedName) is { } code)
        {
            var encodedReturn = decoratedName[code.End..];

            return new FunctionSignature(MarkEnums(parameters, decoratedName),
                                         code.Type,
                                         DecodeReturnType(encodedReturn, decoratedName[1..code.Start]),
                                         IsClassReturnedByValue(encodedReturn));
        }

        return new FunctionSignature(parameters, IsScoped(signature) ? FunctionType.Member : FunctionType.Free, null);
    }

    public static FunctionType? GetFunctionType(ReadOnlySpan<char> decoratedName) => FindFunctionCode(decoratedName)?.Type;

    public static IReadOnlyList<string>? ParseParameters(ReadOnlySpan<char> signature)
        => TryGetParameterList(signature, out var parameters) ? SplitParameters(parameters) : null;

    public static int? CountParameters(ReadOnlySpan<char> signature)
    {
        if (!TryGetParameterList(signature, out var parameters))
        {
            return null;
        }

        if (parameters.IsEmpty)
        {
            return 0;
        }

        var count = 1;

        while (FindTopLevelComma(parameters) is var comma and >= 0)
        {
            count++;

            parameters = parameters[(comma + 1)..];
        }

        return count;
    }

    public static bool HasSameParameters(ReadOnlySpan<char> signature, ReadOnlySpan<char> other)
    {
        TryGetParameterList(signature, out var parameters);

        TryGetParameterList(other, out var otherParameters);

        return parameters.SequenceEqual(otherParameters);
    }

    public static int HashParameters(ReadOnlySpan<char> signature)
        => TryGetParameterList(signature, out var parameters) ? string.GetHashCode(parameters) : 0;

    private static bool TryGetParameterList(ReadOnlySpan<char> signature, out ReadOnlySpan<char> parameters)
    {
        parameters = default;

        var open = FindParameterList(signature);

        if (open < 0)
        {
            return false;
        }

        var depth = 0;

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
                    var inner = signature[(open + 1)..i].Trim();

                    parameters = inner is "void" ? [] : inner;

                    return true;
                }
            }
        }

        return false;
    }

    private static (int Start, int End, FunctionType Type)? FindFunctionCode(ReadOnlySpan<char> decoratedName)
    {
        var offset = 0;

        while (decoratedName[offset..].IndexOf(NameEnd) is var found and >= 0)
        {
            var start = offset + found;

            offset = start + 1;

            var code = decoratedName[(start + NameEnd.Length)..];

            if (code is [var access, 'E', 'A' or 'B' or 'C' or 'D', 'A' or 'Q', ..] && MemberAccessCodes.Contains(access))
            {
                return (start, start + NameEnd.Length + 4, FunctionType.Member);
            }

            if (code is [var staticAccess, 'A' or 'Q', ..] && StaticAccessCodes.Contains(staticAccess))
            {
                return (start, start + NameEnd.Length + 2, FunctionType.Static);
            }

            if (code is ['Y' or 'Z', 'A' or 'Q', ..])
            {
                return (start, start + NameEnd.Length + 2, FunctionType.Free);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> MarkEnums(IReadOnlyList<string> parameters, ReadOnlySpan<char> decoratedName)
    {
        if (GetEnumNames(decoratedName) is not { } enums)
        {
            return parameters;
        }

        return [.. parameters.Select(p => enums.Contains(UnqualifiedName(TargetName(p))) ? $"{EnumPrefix}{p}" : p)];
    }

    private static HashSet<string>? GetEnumNames(ReadOnlySpan<char> decoratedName)
    {
        HashSet<string>? names = null;

        var position = decoratedName.IndexOf(EnumCode);

        while (position >= 0)
        {
            var name = ReadIdentifier(decoratedName[(position + EnumCode.Length)..]);

            var end = position + EnumCode.Length + name.Length;

            var found = !name.IsEmpty && end < decoratedName.Length && decoratedName[end] == '@';

            if (found)
            {
                (names ??= new HashSet<string>(StringComparer.Ordinal)).Add(name.ToString());
            }

            var next = found ? end + 1 : position + 1;

            var offset = decoratedName[next..].IndexOf(EnumCode);

            position = offset < 0 ? -1 : next + offset;
        }

        return names;
    }

    private static string TargetName(string type)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        return string.Join(' ', (indirection < 0 ? type : type[..indirection]).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                                                              .Where(w => w is not ("const" or "volatile")));
    }

    private static string UnqualifiedName(string type)
    {
        var separator = type.LastIndexOf("::", StringComparison.Ordinal);

        return separator < 0 ? type : type[(separator + 2)..];
    }

    private static bool IsScoped(ReadOnlySpan<char> signature)
    {
        var open = FindParameterList(signature);

        return SymbolsHelper.FindClassMethodSeparator(open < 0 ? signature : signature[..open]) > 0;
    }

    private static bool IsClassReturnedByValue(ReadOnlySpan<char> encoded)
        => encoded is ['?', 'A' or 'B' or 'C' or 'D', 'U' or 'V' or 'T', ..];

    private static string? DecodeReturnType(ReadOnlySpan<char> encoded, ReadOnlySpan<char> functionNames)
    {
        if (encoded.IsEmpty)
        {
            return null;
        }

        if (DecodeClass(encoded) is { } className)
        {
            return className;
        }

        if (DecodeEnum(encoded, functionNames) is { } enumName)
        {
            return enumName;
        }

        if (encoded is ['_', var extended, ..])
        {
            return extended switch
            {
                'N' => "bool",
                'J' => "__int64",
                'K' => "unsigned __int64",
                'W' => "wchar_t",
                _ => null
            };
        }

        if (encoded.StartsWith(EnumCode))
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

    private static string? DecodeClass(ReadOnlySpan<char> encoded)
    {
        var position = 2;

        char? indirection = null;

        var isConst = false;

        if (encoded is ['P' or 'Q' or 'A', 'E', ..])
        {
            while (position < encoded.Length && encoded[position] is 'F' or 'I')
            {
                position++;
            }

            if (position >= encoded.Length || encoded[position] is not ('A' or 'B' or 'C' or 'D'))
            {
                return null;
            }

            indirection = encoded[0];

            isConst = encoded[position] is 'B' or 'D';

            position++;
        }
        else if (encoded is not ['?', 'A' or 'B' or 'C' or 'D', ..])
        {
            return null;
        }

        if (position >= encoded.Length || encoded[position] is not ('U' or 'V' or 'T'))
        {
            return null;
        }

        var name = ReadIdentifier(encoded[(position + 1)..]);

        if (name.IsEmpty || !encoded[(position + 1 + name.Length)..].StartsWith(NameEnd))
        {
            return null;
        }

        return indirection is { } pointer ? $"{name}{(isConst ? " const" : "")} {(pointer == 'A' ? '&' : '*')}" : name.ToString();
    }

    private static string? DecodeEnum(ReadOnlySpan<char> encoded, ReadOnlySpan<char> functionNames)
    {
        var text = encoded is ['?', 'A' or 'B' or 'C' or 'D', ..] ? encoded[2..] : encoded;

        if (!text.StartsWith(EnumCode))
        {
            return null;
        }

        var name = ReadIdentifier(text[EnumCode.Length..]);

        var position = EnumCode.Length + name.Length;

        if (name.IsEmpty || position >= text.Length || text[position] != '@')
        {
            return null;
        }

        position++;

        var scopes = new List<Range>();

        while (position < text.Length && text[position] != '@')
        {
            if (char.IsAsciiDigit(text[position]))
            {
                scopes.Add(position..(position + 1));

                position++;

                continue;
            }

            var scope = ReadIdentifier(text[position..]);

            var end = position + scope.Length;

            if (scope.IsEmpty || end >= text.Length || text[end] != '@')
            {
                return null;
            }

            scopes.Add(position..end);

            position = end + 1;
        }

        return position < text.Length ? NameEnum(name.ToString(), text, scopes, functionNames) : null;
    }

    private static string NameEnum(string name, ReadOnlySpan<char> text, List<Range> scopeRanges, ReadOnlySpan<char> functionNames)
    {
        var names = new List<string>(functionNames.ToString().Split('@')) { name };

        var scopes = new List<string>();

        foreach (var range in scopeRanges)
        {
            var scope = text[range];

            if (scope is [var digit] && char.IsAsciiDigit(digit))
            {
                var index = digit - '0';

                if (index >= names.Count)
                {
                    return EnumPrefix.TrimEnd();
                }

                scopes.Add(names[index]);
            }
            else
            {
                var value = scope.ToString();

                scopes.Add(value);

                names.Add(value);
            }
        }

        scopes.Reverse();

        return $"{EnumPrefix}{string.Join("::", scopes.Append(name))}";
    }

    private static ReadOnlySpan<char> ReadIdentifier(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || !(char.IsAsciiLetter(text[0]) || text[0] == '_'))
        {
            return [];
        }

        var length = 1;

        while (length < text.Length && (char.IsAsciiLetterOrDigit(text[length]) || text[length] == '_'))
        {
            length++;
        }

        return text[..length];
    }

    private static int FindParameterList(ReadOnlySpan<char> signature)
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

    private static List<string> SplitParameters(ReadOnlySpan<char> parameters)
    {
        var split = new List<string>();

        if (parameters.IsEmpty)
        {
            return split;
        }

        while (FindTopLevelComma(parameters) is var comma and >= 0)
        {
            split.Add(parameters[..comma].Trim().ToString());

            parameters = parameters[(comma + 1)..];
        }

        split.Add(parameters.Trim().ToString());

        return split;
    }

    private static int FindTopLevelComma(ReadOnlySpan<char> parameters)
    {
        var depth = 0;

        for (var i = 0; i < parameters.Length; i++)
        {
            var character = parameters[i];

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
                return i;
            }
        }

        return -1;
    }
}
