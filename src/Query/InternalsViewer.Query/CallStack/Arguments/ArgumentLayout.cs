namespace InternalsViewer.Query.CallStack.Arguments;

public sealed record ArgumentLayout(IReadOnlyList<ArgumentSlot> Slots, string? ReturnType)
{
    public const int CapturedSlots = 8;

    private const int StackArgumentOffset = 0x28;

    private static readonly string[] Registers = ["RCX", "RDX", "R8", "R9"];

    public static ArgumentLayout For(FunctionSignature signature)
    {
        var slots = new List<ArgumentSlot>();

        var position = 0;

        if (signature.Kind == FunctionKind.Member)
        {
            slots.Add(new ArgumentSlot("this", "this", ArgumentLocation.Register, 0, Registers[0]));

            position = 1;
        }

        for (var i = 0; i < signature.Parameters.Count; i++, position++)
        {
            slots.Add(Slot($"Argument {i + 1}", signature.Parameters[i], position));
        }

        return new ArgumentLayout(slots, signature.ReturnType);
    }

    public static int StackOffset(int position) => StackArgumentOffset + (position - Registers.Length) * sizeof(ulong);

    private static ArgumentSlot Slot(string name, string type, int position)
    {
        if (position >= CapturedSlots || type == "...")
        {
            var location = position >= Registers.Length ? $"[RSP+0x{StackOffset(position):X}]" : "Stack";

            return new ArgumentSlot(name, type, ArgumentLocation.NotCaptured, position, location);
        }

        if (position >= Registers.Length)
        {
            return new ArgumentSlot(name, type, ArgumentLocation.Stack, position, $"[RSP+0x{StackOffset(position):X}]");
        }

        return ArgumentValue.IsFloating(type)
            ? new ArgumentSlot(name, type, ArgumentLocation.FloatingRegister, position, $"XMM{position}")
            : new ArgumentSlot(name, type, ArgumentLocation.Register, position, Registers[position]);
    }
}
