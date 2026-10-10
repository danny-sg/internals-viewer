using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.CallStack.WinDbg;

/// <summary>
/// Builds WinDbg commands targeting a call stack frame or a class member's function, class or address
/// </summary>
/// <remarks>
/// A target with a symbol is addressed as <c>module!Class::Method</c>. One without, a frame whose symbol did not
/// resolve, is addressed by its module-relative address instead, so every command still lands on the right code.
/// An overloaded member is addressed the same way, as its name alone would not pick out the overload chosen.
/// Names that go beyond identifiers and scope separators, such as template instantiations, are wrapped in WinDbg's
/// <c>@!""</c> quoting wherever the command parses an expression.
/// </remarks>
public static class WinDbgCommands
{
    /// <summary>
    /// Clears every breakpoint in the session
    /// </summary>
    public const string ClearBreakpoints = "bc *";

    public static string? For(string command, WinDbgTarget target) => command switch
    {
        "Breakpoint" => Breakpoint(target),
        "BreakpointWithStack" => BreakpointWithStack(target),
        "BreakpointAtFrame" => BreakpointAtFrame(target),
        "BreakpointOnAllOverloads" => BreakpointOnAllOverloads(target),
        "ExamineSymbol" => ExamineSymbol(target),
        "DisplayType" => DisplayType(target),
        "ListClassSymbols" => ListClassSymbols(target),
        _ => null
    };

    /// <summary>
    /// The frame's function as WinDbg names it, <c>module!Class::Method</c>, or <c>module+0xRVA</c> when unresolved
    /// </summary>
    public static string Symbol(WinDbgTarget target) => target.Name is { } name ? $"{target.Module}!{name}" : Address(target);

    /// <summary>
    /// Breaks whenever the frame's function is entered
    /// </summary>
    public static string Breakpoint(WinDbgTarget target) => $"bp {Expression(target)}";

    /// <summary>
    /// Breaks on entry, prints the stack that got there, and carries on
    /// </summary>
    public static string BreakpointWithStack(WinDbgTarget target) => $"bp {Expression(target)} \"k; g\"";

    /// <summary>
    /// Breaks on entry, prints its arguments read from the signature, and carries on
    /// </summary>
    public static string DumpArguments(WinDbgTarget target, string? decoratedName = null) =>
        DumpArgumentsFromSignature(target, decoratedName, resume: true);

    /// <summary>
    /// Breaks on entry, prints its arguments read from the signature, and stays broken
    /// </summary>
    public static string DumpArgumentsAndBreak(WinDbgTarget target, string? decoratedName = null) =>
        DumpArgumentsFromSignature(target, decoratedName, resume: false);

    /// <summary>
    /// Breaks at the exact address captured in this frame, the instruction after the call the frame was waiting on
    /// </summary>
    public static string BreakpointAtFrame(WinDbgTarget target) =>
        target.Name is not null && target.Offset is { } offset
            ? $"bp {Expression(target)}+0x{offset:X}"
            : $"bp {Address(target)}";

    /// <summary>
    /// Breaks on every overload sharing the member's name
    /// </summary>
    public static string BreakpointOnAllOverloads(WinDbgTarget target) => $"bm {Symbol(target)}";

    /// <summary>
    /// Lists the address of the frame's function, and every overload sharing its name
    /// </summary>
    public static string ExamineSymbol(WinDbgTarget target) => $"x {Symbol(target)}";

    /// <summary>
    /// Dumps the layout of the frame's class, or null when the frame has no class
    /// </summary>
    public static string? DisplayType(WinDbgTarget target) =>
        target.ClassName is { } className ? $"dt {target.Module}!{className}" : null;

    /// <summary>
    /// Lists every symbol on the frame's class, or null when the frame has no class
    /// </summary>
    public static string? ListClassSymbols(WinDbgTarget target) =>
        target.ClassName is { } className ? $"x {target.Module}!{className}::*" : null;

    private static string DumpArguments(WinDbgTarget target, bool resume)
    {
        var action = resume ? ".echo Arguments; r rcx, rdx, r8, r9; g" : ".echo Arguments; r rcx, rdx, r8, r9";

        return $"bp {Expression(target)} \"{action}\"";
    }

    /// <summary>
    /// A breakpoint that reads each argument from the signature, falling back to the registers when it cannot
    /// </summary>
    /// <remarks>
    /// The types come from the undecorated signature, so the routine maps them onto the x64 calling convention itself:
    /// an assumed <c>this</c> in <c>rcx</c>, then integer and pointer arguments across <c>rdx</c>/<c>r8</c>/<c>r9</c>
    /// and the stack, floating point arguments in the matching <c>xmm</c> register, and strings dumped as text. A static
    /// member, a floating point argument past the fourth, and a by-value struct return are not accounted for.
    /// </remarks>
    private static string DumpArgumentsFromSignature(WinDbgTarget target, string? decoratedName, bool resume)
    {
        if (target.Signature is not { } signature || ArgumentDumpAction(signature, decoratedName) is not { } action)
        {
            return DumpArguments(target, resume);
        }

        return $"bp {Expression(target)} \"{(resume ? $"{action}; g" : action)}\"";
    }

    private static string? ArgumentDumpAction(string signature, string? decoratedName)
    {
        if (FunctionSignature.Parse(signature, decoratedName) is not { } parsed)
        {
            return null;
        }

        var parts = new List<string> { ".echo === Arguments ===" };

        var argument = 0;

        var function = decoratedName is null ? parsed with { Kind = FunctionKind.Member } : parsed;

        foreach (var slot in ArgumentLayout.For(function).Slots)
        {
            parts.Add(slot.Type == "this" ? Printf("this = rcx = %p", "@rcx") : ArgumentLine(slot, ++argument));
        }

        return string.Join("; ", parts);
    }

    private static string ArgumentLine(ArgumentSlot slot, int argument)
    {
        var type = slot.Type;

        var pointer = ArgumentValue.IsPointer(type);

        var (location, expression) = slot.Location switch
        {
            ArgumentLocation.Register => (IntegerRegister(slot.Index), $"@{IntegerRegister(slot.Index)}"),
            ArgumentLocation.FloatingRegister => ($"xmm{slot.Index}", $"@xmm{slot.Index}"),
            _ => ($"[rsp+0x{ArgumentLayout.StackOffset(slot.Index):X}]", $"poi(@rsp+0x{ArgumentLayout.StackOffset(slot.Index):X})")
        };

        var format = pointer
            ? IsWideString(type) ? "%mu" : IsAnsiString(type) ? "%ma" : "%p"
            : slot.Location == ArgumentLocation.FloatingRegister ? "%f" : "%p";

        return Printf($"arg{argument} ({type}) = {location} = {format}", expression);
    }

    private static string Printf(string format, string argument) => $".printf \\\"{format}\\\\n\\\", {argument}";

    private static string IntegerRegister(int position) => position switch
    {
        0 => "rcx",
        1 => "rdx",
        2 => "r8",
        _ => "r9"
    };

    private static bool IsWideString(string type) => type.Contains("wchar_t") && type.Contains('*');

    private static bool IsAnsiString(string type) =>
        type.Contains("char") && !type.Contains("wchar_t") && !type.Contains("unsigned char") && type.Contains('*');

    private static string Expression(WinDbgTarget target) =>
        target.Name is null || target.IsOverloaded ? Address(target) : Quote($"{target.Module}!{target.Name}");

    private static string Address(WinDbgTarget target) => $"{target.Module}+0x{target.Rva:X}";

    private static string Quote(string expression) =>
        expression.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or ':' or '!' or '+')
            ? expression
            : $"@!\"{expression}\"";
}
