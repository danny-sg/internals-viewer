namespace InternalsViewer.Query.Callstack;

public sealed record ResolvedCallstackFrame(string? ClassName, string? MethodName, uint? Offset);

public class ResolvedCallstackFrameParser
{
    public static ResolvedCallstackFrame Parse(string value)
    {
        var plusIndex = value.LastIndexOf('+');

        var symbolPart = plusIndex >= 0
            ? value[..plusIndex]
            : value;

        var offsetPart = plusIndex >= 0
            ? value[(plusIndex + 1)..]
            : null;

        var separator = symbolPart.LastIndexOf("::", StringComparison.Ordinal);

        var className = separator >= 0 ? symbolPart[..separator] : null;

        var methodName = separator >= 0 ? symbolPart[(separator + 2)..] : symbolPart;

        uint? offset = null;

        if (!string.IsNullOrEmpty(offsetPart))
        {
            offsetPart = offsetPart.Trim();

            if (offsetPart.StartsWith("0x",
                    StringComparison.OrdinalIgnoreCase))
            {
                offsetPart = offsetPart[2..];
            }

            if (uint.TryParse(offsetPart,
                              System.Globalization.NumberStyles.HexNumber,
                              null,
                              out var offsetValue))
            {
                offset = offsetValue;
            }
        }

        return new ResolvedCallstackFrame(className, methodName, offset);
    }
}