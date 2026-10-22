using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Arguments;

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

    public static string GetStackLocation(int position) => $"[RSP+0x{GetStackOffset(position):X}]";

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
