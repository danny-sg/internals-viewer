namespace InternalsViewer.Query.CallStack;

public static class QualifiedSymbol
{
    public static string Of(CallstackFrame frame)
        => FunctionName(frame) is { } name ? $"{frame.Module}!{name}" : $"{frame.Module}+0x{frame.Rva:X}";

    public static string Of(ClassMemberReference member)
        => member.ClassName.Length > 0
            ? $"{member.Member.Module}!{member.ClassName}::{member.Member.Name}"
            : $"{member.Member.Module}!{member.Member.Name}";

    private static string? FunctionName(CallstackFrame frame)
    {
        if (frame.Resolved is not { RawSymbol.Length: > 0 } resolved || resolved.RawSymbol.StartsWith("0x"))
        {
            return null;
        }

        var plusIndex = resolved.RawSymbol.LastIndexOf('+');

        return plusIndex > 0 ? resolved.RawSymbol[..plusIndex] : resolved.RawSymbol;
    }
}
