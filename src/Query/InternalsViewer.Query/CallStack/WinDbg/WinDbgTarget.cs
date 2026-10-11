namespace InternalsViewer.Query.CallStack.WinDbg;

public readonly record struct WinDbgTarget(string Module,
                                           string? Name,
                                           string? ClassName,
                                           uint Rva,
                                           bool IsOverloaded,
                                           uint? Offset = null,
                                           string? Signature = null)
{
    public static WinDbgTarget From(CallstackFrame frame)
    {
        var name = SymbolName(frame);

        var className = name is not null && frame.Resolved?.ClassName is { Length: > 0 } resolvedClass
            ? resolvedClass
            : null;

        return new WinDbgTarget(frame.Module, name, className, frame.Rva, IsOverloaded: false, frame.Resolved?.Offset);
    }

    public static WinDbgTarget From(ClassMemberReference member) =>
        new(member.Member.Module,
            member.ClassName.Length > 0 ? $"{member.ClassName}::{member.Member.Name}" : member.Member.Name,
            member.ClassName.Length > 0 ? member.ClassName : null,
            member.Member.Rva,
            member.IsOverloaded,
            Signature: member.Member.Signature);

    private static string? SymbolName(CallstackFrame frame)
    {
        if (frame.Resolved is not { RawSymbol.Length: > 0 } resolved || resolved.RawSymbol.StartsWith("0x"))
        {
            return null;
        }

        var plusIndex = resolved.RawSymbol.LastIndexOf('+');

        return plusIndex > 0 ? resolved.RawSymbol[..plusIndex] : resolved.RawSymbol;
    }
}
