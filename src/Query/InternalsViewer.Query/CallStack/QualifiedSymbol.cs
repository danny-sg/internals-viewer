namespace InternalsViewer.Query.CallStack;

public static class QualifiedSymbol
{
    public static string Of(CallstackFrame frame)
        => frame.Function is { Name.Length: > 0 } function && !function.Name.StartsWith("0x", StringComparison.Ordinal)
            ? $"{frame.Module.Name}!{function.Name}"
            : $"{frame.Module.Name}+0x{frame.Rva:X}";

    public static string Of(ClassMemberReference member)
        => member.ClassName.Length > 0
            ? $"{member.Member.Module}!{member.ClassName}::{member.Member.Name}"
            : $"{member.Member.Module}!{member.Member.Name}";
}
