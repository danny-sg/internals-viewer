using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Arguments;

/// <summary>
/// Models function arguments and their layout to slots (name/type/location/index/source)
/// </summary>
/// <remarks>
/// Functions use the Windows x64 calling conventions. The convention defines where an argument is located. This can be:
///
/// Sources:
/// 
///     Position 0-3
///     
///     - Registers
///         - Integers/pointers: RCX, RDX, R8 and R9
///         - Floating point: XMM0, XMM1, XMM2, and XMM3
///     
///     Position 4-15
///     
///     - Stack offset
///     
///     Position >= 16 or variadic argument (... signifying variable number of arguments)
///     
///     - Not Captured
///
/// Layout depends on if the function is static or a class member as the first register for a member is always RCX as 'this'.
///
/// A hidden return buffer pointer, when present, also takes a position and shifts the remaining parameters.
///
/// Return types:
/// 
///     - Void - None
///     - Integer/pointers: RAX
///     - Floating point: XMM0
///     - Return buffers: RAX as a pointer
/// </remarks>
public sealed record ArgumentLayout(IReadOnlyList<ArgumentSlot> Slots, string? ReturnType)
{
    public const int RegisterCount = 4;

    public const int CapturedSlots = 16;

    public const int ReturnRegister = CapturedSlots;

    public const int HomeSpaceSize = RegisterCount * sizeof(ulong);

    public const int StackArgumentOffset = sizeof(ulong) + HomeSpaceSize;

    public const string ThisRegisterName = "RCX";

    public const string ReturnRegisterName = "RAX";

    public const string ReturnBuffer = "Return Buffer";

    private const string VoidType = "void";

    private const string VariadicType = "...";

    public static IReadOnlyList<string> Registers { get; } = [ThisRegisterName, "RDX", "R8", "R9"];

    public static ArgumentLayout GetLayout(FunctionSignature signature)
    {
        var slots = new List<ArgumentSlot>();

        var position = 0;

        if (signature.Type == FunctionType.Member)
        {
            slots.Add(new ArgumentSlot("this", "this", ArgumentLocation.Register, 0, ThisRegisterName));

            position = 1;
        }

        if (signature.HasReturnBuffer)
        {
            slots.Add(GetSlot(ReturnBuffer, $"{signature.ReturnType ?? VoidType} *", position));

            position++;
        }

        for (var i = 0; i < signature.Parameters.Count; i++, position++)
        {
            slots.Add(GetSlot(string.Empty, signature.Parameters[i], position));
        }

        return new ArgumentLayout(slots, signature.ReturnType);
    }

    public static string GetStackLocation(int position)
        => $"[RSP+0x{GetStackOffset(position):X}]";

    public bool IsDeclared(string location)
    {
        if (location == ReturnRegisterName)
        {
            return ReturnType is not { } returnType || (returnType != VoidType && !ArgumentValue.IsFloating(returnType));
        }

        foreach (var slot in Slots)
        {
            if (slot.Type == VariadicType && GetRegisterIndex(location) >= slot.Index)
            {
                return true;
            }

            if (slot.Location is ArgumentLocation.Register or ArgumentLocation.Stack && slot.Source == location)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetStackOffset(int position) => StackArgumentOffset + (position - RegisterCount) * sizeof(ulong);

    private static int GetRegisterIndex(string location)
    {
        for (var index = 0; index < RegisterCount; index++)
        {
            if (Registers[index] == location)
            {
                return index;
            }
        }

        return -1;
    }

    private static ArgumentSlot GetSlot(string name, string type, int position)
    {
        if (position >= CapturedSlots || type == VariadicType)
        {
            var location = position >= RegisterCount ? GetStackLocation(position) : "Stack";

            return new ArgumentSlot(name, type, ArgumentLocation.NotCaptured, position, location);
        }

        if (position >= RegisterCount)
        {
            return new ArgumentSlot(name, type, ArgumentLocation.Stack, position, GetStackLocation(position));
        }

        return ArgumentValue.IsFloating(type)
            ? new ArgumentSlot(name, type, ArgumentLocation.FloatingRegister, position, $"XMM{position}")
            : new ArgumentSlot(name, type, ArgumentLocation.Register, position, Registers[position]);
    }
}
