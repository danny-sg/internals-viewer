namespace InternalsViewer.Query.CallStack.Arguments;

public static class ArgumentType
{
    public static bool IsFloating(string type) => Normalise(type) is "float" or "double" or "long double";

    public static bool IsPointer(string type) => type.TrimEnd() is { Length: > 0 } trimmed && trimmed[^1] is '*' or '&';

    public static string Normalise(string type)
        => string.Join(' ', type.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .Where(part => part is not ("const" or "volatile" or "__ptr64")));
}
