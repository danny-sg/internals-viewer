using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

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

    /// <summary>
    /// The frame's function as WinDbg names it, <c>module!Class::Method</c>, or <c>module+0xRVA</c> when unresolved
    /// </summary>
    public static string Symbol(CallstackFrame frame) => Symbol(Target.From(frame));

    /// <summary>
    /// The member as WinDbg names it, <c>module!Class::Member</c>
    /// </summary>
    public static string Symbol(ClassMemberRow member) => Symbol(Target.From(member));

    /// <summary>
    /// Breaks whenever the frame's function is entered
    /// </summary>
    public static string Breakpoint(CallstackFrame frame) => Breakpoint(Target.From(frame));

    /// <summary>
    /// Breaks whenever the member is entered, by address when the name is overloaded
    /// </summary>
    public static string Breakpoint(ClassMemberRow member) => Breakpoint(Target.From(member));

    /// <summary>
    /// Breaks on entry, prints the stack that got there, and carries on
    /// </summary>
    public static string BreakpointWithStack(CallstackFrame frame) => BreakpointWithStack(Target.From(frame));

    /// <summary>
    /// Breaks on entry to the member, prints the stack that got there, and carries on
    /// </summary>
    public static string BreakpointWithStack(ClassMemberRow member) => BreakpointWithStack(Target.From(member));

    /// <summary>
    /// Breaks on entry, prints its arguments read from the signature, and carries on
    /// </summary>
    public static string DumpArguments(CallstackFrame frame, string? signature, string? decoratedName = null) =>
        DumpArgumentsFromSignature(Target.From(frame), signature, decoratedName, resume: true);

    /// <summary>
    /// Breaks on entry, prints its arguments read from the signature, and stays broken
    /// </summary>
    public static string DumpArgumentsAndBreak(CallstackFrame frame, string? signature, string? decoratedName = null) =>
        DumpArgumentsFromSignature(Target.From(frame), signature, decoratedName, resume: false);

    /// <summary>
    /// Breaks on entry to the member, prints its arguments read from the signature, and carries on
    /// </summary>
    public static string DumpArguments(ClassMemberRow member, string? decoratedName = null) =>
        DumpArgumentsFromSignature(Target.From(member), member.Signature, decoratedName, resume: true);

    /// <summary>
    /// Breaks on entry to the member, prints its arguments read from the signature, and stays broken
    /// </summary>
    public static string DumpArgumentsAndBreak(ClassMemberRow member, string? decoratedName = null) =>
        DumpArgumentsFromSignature(Target.From(member), member.Signature, decoratedName, resume: false);

    /// <summary>
    /// Breaks at the exact address captured in this frame, the instruction after the call the frame was waiting on
    /// </summary>
    public static string BreakpointAtFrame(CallstackFrame frame)
    {
        var target = Target.From(frame);

        return target.Name is not null && frame.Resolved?.Offset is { } offset
            ? $"bp {Expression(target)}+0x{offset:X}"
            : $"bp {Address(target)}";
    }

    /// <summary>
    /// Breaks on every overload sharing the member's name
    /// </summary>
    public static string BreakpointOnAllOverloads(ClassMemberRow member) => $"bm {Symbol(Target.From(member))}";

    /// <summary>
    /// Lists the address of the frame's function, and every overload sharing its name
    /// </summary>
    public static string ExamineSymbol(CallstackFrame frame) => ExamineSymbol(Target.From(frame));

    /// <summary>
    /// Lists the address of the member, and every overload sharing its name
    /// </summary>
    public static string ExamineSymbol(ClassMemberRow member) => ExamineSymbol(Target.From(member));

    /// <summary>
    /// Dumps the layout of the frame's class, or null when the frame has no class
    /// </summary>
    public static string? DisplayType(CallstackFrame frame) => DisplayType(Target.From(frame));

    /// <summary>
    /// Dumps the layout of the member's class
    /// </summary>
    public static string? DisplayType(ClassMemberRow member) => DisplayType(Target.From(member));

    /// <summary>
    /// Lists every symbol on the frame's class, or null when the frame has no class
    /// </summary>
    public static string? ListClassSymbols(CallstackFrame frame) => ListClassSymbols(Target.From(frame));

    /// <summary>
    /// Lists every symbol on the member's class
    /// </summary>
    public static string? ListClassSymbols(ClassMemberRow member) => ListClassSymbols(Target.From(member));

    private static string Symbol(Target target) => target.Name is { } name ? $"{target.Module}!{name}" : Address(target);

    private static string Breakpoint(Target target) => $"bp {Expression(target)}";

    private static string BreakpointWithStack(Target target) => $"bp {Expression(target)} \"k; g\"";

    private static string DumpArguments(Target target, bool resume)
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
    private static string DumpArgumentsFromSignature(Target target, string? signature, string? decoratedName, bool resume)
    {
        if (signature is null || ArgumentDumpAction(signature, decoratedName) is not { } action)
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

    private static string ExamineSymbol(Target target) => $"x {Symbol(target)}";

    private static string? DisplayType(Target target) =>
        target.ClassName is { } className ? $"dt {target.Module}!{className}" : null;

    private static string? ListClassSymbols(Target target) =>
        target.ClassName is { } className ? $"x {target.Module}!{className}::*" : null;

    private static string Expression(Target target) =>
        target.Name is null || target.IsOverloaded ? Address(target) : Quote($"{target.Module}!{target.Name}");

    private static string Address(Target target) => $"{target.Module}+0x{target.Rva:X}";

    private static string Quote(string expression) =>
        expression.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or ':' or '!' or '+')
            ? expression
            : $"@!\"{expression}\"";

    private readonly record struct Target(string Module, string? Name, string? ClassName, uint Rva, bool IsOverloaded)
    {
        public static Target From(CallstackFrame frame)
        {
            var name = SymbolName(frame);

            var className = name is not null && frame.Resolved?.ClassName is { Length: > 0 } resolvedClass
                ? resolvedClass
                : null;

            return new Target(frame.Module, name, className, frame.Rva, IsOverloaded: false);
        }

        public static Target From(ClassMemberRow member) =>
            new(member.Member.Module,
                member.ClassName.Length > 0 ? $"{member.ClassName}::{member.Member.Name}" : member.Member.Name,
                member.ClassName.Length > 0 ? member.ClassName : null,
                member.Member.Rva,
                member.IsOverloaded);

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
}
