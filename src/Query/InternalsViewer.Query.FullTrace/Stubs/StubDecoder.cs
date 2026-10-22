using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Stubs.Enums;

namespace InternalsViewer.Query.FullTrace.Stubs;

internal static class StubDecoder
{
    private const int MaximumInstructions = 8;

    private const byte RexMask = 0xF0;

    private const byte RexPrefix = 0x40;

    private const byte RexWide = 0x08;

    private const byte RexRegister = 0x04;

    private const byte RexIndex = 0x02;

    private const byte RexBase = 0x01;

    private const int ExtendedRegister = 8;

    private const byte MoveToRegister = 0x8B;

    private const byte MoveFromRegister = 0x89;

    private const byte LoadAddress = 0x8D;

    private const byte ImmediateGroup8 = 0x83;

    private const byte ImmediateGroup32 = 0x81;

    private const byte IndirectGroup = 0xFF;

    private const byte JumpRelative32 = 0xE9;

    private const byte JumpRelative8 = 0xEB;

    private const int AddExtension = 0;

    private const int SubtractExtension = 5;

    private const int JumpExtension = 4;

    private const int RegisterMode = 3;

    private const int SibOperand = 4;

    private const int NoIndex = 4;

    private const int NoBase = 5;

    private const int StackPointer = 4;

    public static Stub? Decode(ReadOnlySpan<byte> code, ulong address)
    {
        var reader = new CodeReader(code);

        var operations = new List<StubOperation>();

        for (var count = 0; count < MaximumInstructions; count++)
        {
            switch (DecodeInstruction(ref reader, address, operations))
            {
                case null:
                    return null;

                case true:
                    return new Stub(address, [.. operations]);
            }
        }

        return null;
    }

    private static bool? DecodeInstruction(ref CodeReader reader, ulong address, List<StubOperation> operations)
    {
        if (!reader.TryReadByte(out var opcode))
        {
            return null;
        }

        var rex = 0;

        if ((opcode & RexMask) == RexPrefix)
        {
            rex = opcode;

            if (!reader.TryReadByte(out opcode))
            {
                return null;
            }
        }

        switch (opcode)
        {
            case JumpRelative32 when reader.TryReadInt32(out var relative):
                return AddJump(operations, (ulong)((long)address + reader.Position + relative));

            case JumpRelative8 when reader.TryReadSByte(out var relative):
                return AddJump(operations, (ulong)((long)address + reader.Position + relative));

            case MoveToRegister or MoveFromRegister or LoadAddress or ImmediateGroup8 or ImmediateGroup32 or IndirectGroup
                when TryReadOperand(ref reader, rex, out var operand):
                return DecodeOperation(ref reader, address, rex, opcode, operand, operations);

            default:
                return null;
        }
    }

    private static bool? DecodeOperation(ref CodeReader reader,
                                         ulong address,
                                         int rex,
                                         byte opcode,
                                         Operand operand,
                                         List<StubOperation> operations)
    {
        var isWide = (rex & RexWide) != 0;

        var absolute = (ulong)((long)address + reader.Position + operand.Displacement);

        var memory = operand.IsRipRelative
            ? new StubOperation(StubOperationType.Load, 0, StubOperation.AbsoluteSource, 0, absolute)
            : new StubOperation(StubOperationType.Load, 0, operand.Base, operand.Displacement, 0);

        StubOperation operation;

        switch (opcode)
        {
            case MoveToRegister when isWide && operand.IsRegister:
                operation = new StubOperation(StubOperationType.Copy, operand.Register, operand.Base, 0, 0);
                break;

            case MoveToRegister when isWide:
                operation = memory with { Destination = operand.Register };
                break;

            case LoadAddress when isWide && !operand.IsRegister && !operand.IsRipRelative:
                operation = new StubOperation(StubOperationType.Add, operand.Register, operand.Base, operand.Displacement, 0);
                break;

            case MoveFromRegister when isWide && operand.IsRegister:
                operation = new StubOperation(StubOperationType.Copy, operand.Base, operand.Register, 0, 0);
                break;

            case ImmediateGroup8 or ImmediateGroup32
                when isWide && operand.IsRegister && operand.Extension is AddExtension or SubtractExtension:
            {
                if (!TryReadImmediate(ref reader, opcode, out var immediate))
                {
                    return null;
                }

                var displacement = operand.Extension == SubtractExtension ? -immediate : immediate;

                operation = new StubOperation(StubOperationType.Add, operand.Base, operand.Base, displacement, 0);
                break;
            }

            case IndirectGroup when operand.Extension == JumpExtension && operand.IsRegister:
                operations.Add(new StubOperation(StubOperationType.JumpRegister, 0, operand.Base, 0, 0));
                return true;

            case IndirectGroup when operand.Extension == JumpExtension:
                operations.Add(memory with { Type = StubOperationType.JumpMemory });
                return true;

            default:
                return null;
        }

        if (operation.Destination == StackPointer)
        {
            return null;
        }

        operations.Add(operation);

        return false;
    }

    private static bool AddJump(List<StubOperation> operations, ulong target)
    {
        operations.Add(new StubOperation(StubOperationType.JumpDirect, 0, StubOperation.AbsoluteSource, 0, target));

        return true;
    }

    private static bool TryReadImmediate(ref CodeReader reader, byte opcode, out long immediate)
    {
        if (opcode == ImmediateGroup8)
        {
            var isRead = reader.TryReadSByte(out var value);

            immediate = value;

            return isRead;
        }

        var isWordRead = reader.TryReadInt32(out var word);

        immediate = word;

        return isWordRead;
    }

    private static bool TryReadOperand(ref CodeReader reader, int rex, out Operand operand)
    {
        operand = default;

        if (!reader.TryReadByte(out var modrm))
        {
            return false;
        }

        var mode = modrm >> 6;

        var extension = (modrm >> 3) & 7;

        var register = extension | ((rex & RexRegister) != 0 ? ExtendedRegister : 0);

        var rm = modrm & 7;

        var baseExtension = (rex & RexBase) != 0 ? ExtendedRegister : 0;

        if (mode == RegisterMode)
        {
            operand = new Operand(register, extension, rm | baseExtension, 0, IsRegister: true, IsRipRelative: false);

            return true;
        }

        var baseRegister = rm | baseExtension;

        var isRipRelative = false;

        if (rm == SibOperand)
        {
            if (!reader.TryReadByte(out var sib)
                || ((sib >> 3) & 7) != NoIndex
                || (rex & RexIndex) != 0
                || (mode == 0 && (sib & 7) == NoBase))
            {
                return false;
            }

            baseRegister = (sib & 7) | baseExtension;
        }
        else if (mode == 0 && rm == NoBase)
        {
            isRipRelative = true;
        }

        long displacement = 0;

        if (mode == 1)
        {
            if (!reader.TryReadSByte(out var small))
            {
                return false;
            }

            displacement = small;
        }
        else if (mode == 2 || isRipRelative)
        {
            if (!reader.TryReadInt32(out var large))
            {
                return false;
            }

            displacement = large;
        }

        operand = new Operand(register, extension, baseRegister, displacement, IsRegister: false, isRipRelative);

        return true;
    }

    private readonly record struct Operand(int Register, int Extension, int Base, long Displacement, bool IsRegister, bool IsRipRelative);

    private ref struct CodeReader
    {
        public CodeReader(ReadOnlySpan<byte> code)
        {
            Code = code;
        }

        public int Position { get; private set; }

        private ReadOnlySpan<byte> Code { get; }

        public bool TryReadByte(out byte value)
        {
            value = 0;

            if (Position >= Code.Length)
            {
                return false;
            }

            value = Code[Position++];

            return true;
        }

        public bool TryReadSByte(out sbyte value)
        {
            var isRead = TryReadByte(out var raw);

            value = (sbyte)raw;

            return isRead;
        }

        public bool TryReadInt32(out int value)
        {
            value = 0;

            if (Position + sizeof(int) > Code.Length)
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt32LittleEndian(Code[Position..]);

            Position += sizeof(int);

            return true;
        }
    }
}
