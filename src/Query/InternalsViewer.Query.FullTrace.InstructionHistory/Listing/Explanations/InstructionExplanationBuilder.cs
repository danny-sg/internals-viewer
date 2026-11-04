using System.Text;
using Iced.Intel;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Helpers;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Explanations;

internal sealed class InstructionExplanationBuilder(Formatter formatter)
{
    private const string InvalidExplanation = "These bytes are not an x64 instruction the decoder recognises.";

    private const string TruncatedExplanation = "The recording holds only part of this instruction, so it cannot be decoded.";

    private const string MissingExplanation = "No code was recorded at this address.";

    private const string LockSentence = " The lock prefix makes the whole change in one step, so other threads never see it half done.";

    private const string ThreadDataPhrase = "data that belongs to the current thread";

    private static string[] Ordinals { get; } = ["first", "second", "third", "fourth"];

    private InstructionInfoFactory InfoFactory { get; } = new();

    private StringOutput Output { get; } = new();

    private StringBuilder Text { get; } = new();

    private StringBuilder Expression { get; } = new();

    private List<Register> ReadRegisters { get; } = [];

    private List<Register> WrittenRegisters { get; } = [];

    private int AnnotatedSlots { get; set; }

    public string GetExplanation(in DecodedInstruction decoded, ArgumentRegisterUse arguments)
    {
        if (decoded.Error == DecoderError.InvalidInstruction)
        {
            return InvalidExplanation;
        }

        if (decoded.Error != DecoderError.None)
        {
            return decoded.CodeLength == 0 ? MissingExplanation : TruncatedExplanation;
        }

        var instruction = decoded.Instruction;

        var info = InfoFactory.GetInfo(instruction);

        var template = InstructionTemplateLookup.GetTemplate(instruction);

        Text.Clear();

        AnnotatedSlots = 0;

        if (template is null)
        {
            AppendRegisterUse(instruction, info);
        }
        else
        {
            AppendTemplate(template, instruction, info, arguments);
        }

        if (instruction.HasLockPrefix)
        {
            Text.Append(LockSentence);
        }

        return Text.ToString();
    }

    private static string GetAccumulator(int size) => size switch
    {
        1 => "al",
        2 => "ax",
        4 => "eax",
        _ => "rax"
    };

    private static string GetExtension(int size) => size switch
    {
        1 => "ah",
        2 => "dx",
        4 => "edx",
        _ => "rdx"
    };

    private static int GetFirstOperandSize(in Instruction instruction)
    {
        if (instruction.OpCount == 0)
        {
            return 8;
        }

        return instruction.GetOpKind(0) == OpKind.Register
            ? instruction.GetOpRegister(0).GetSize()
            : instruction.MemorySize.GetSize();
    }

    private static string GetItems(in Instruction instruction)
    {
        var size = instruction.MemorySize.GetSize();

        return size <= 1 ? "bytes" : $"{size}-byte items";
    }

    private void AppendTemplate(string template, in Instruction instruction, in InstructionInfo info, ArgumentRegisterUse arguments)
    {
        var position = 0;

        while (position < template.Length)
        {
            var open = template.IndexOf('{', position);

            if (open < 0)
            {
                Text.Append(template, position, template.Length - position);

                return;
            }

            var close = template.IndexOf('}', open);

            Text.Append(template, position, open - position);

            Text.Append(GetTokenText(template.AsSpan(open + 1, close - open - 1), instruction, info, arguments));

            position = close + 1;
        }
    }

    private string GetTokenText(ReadOnlySpan<char> token,
                                in Instruction instruction,
                                in InstructionInfo info,
                                ArgumentRegisterUse arguments) => token switch
    {
        "0" => GetOperandPhrase(instruction, info, arguments, 0),
        "1" => GetOperandPhrase(instruction, info, arguments, 1),
        "2" => GetOperandPhrase(instruction, info, arguments, 2),
        "target" => GetTargetPhrase(instruction, info, arguments),
        "condition" => ConditionPhraseLookup.GetPhrase(instruction.ConditionCode),
        "address" => GetAddressPhrase(instruction, arguments),
        "rax" => GetAccumulator(GetFirstOperandSize(instruction)),
        "rdx" => GetExtension(GetFirstOperandSize(instruction)),
        "items" => GetItems(instruction),
        _ => string.Empty
    };

    private string GetOperandPhrase(in Instruction instruction, in InstructionInfo info, ArgumentRegisterUse arguments, int operand)
    {
        if (operand >= instruction.OpCount)
        {
            return string.Empty;
        }

        return instruction.GetOpKind(operand) switch
        {
            OpKind.Register => GetRegisterPhrase(instruction.GetOpRegister(operand), info.GetOpAccess(operand), arguments),
            OpKind.Memory => GetMemoryPhrase(instruction, arguments),
            _ => FormatOperand(instruction, operand)
        };
    }

    private string GetTargetPhrase(in Instruction instruction, in InstructionInfo info, ArgumentRegisterUse arguments)
    {
        if (instruction.OpCount == 0)
        {
            return string.Empty;
        }

        return instruction.GetOpKind(0) switch
        {
            OpKind.Register => $"the address held in {GetRegisterPhrase(instruction.GetOpRegister(0), info.GetOpAccess(0), arguments)}",
            OpKind.Memory => $"the address stored in {GetMemoryPhrase(instruction, arguments)}",
            _ => FormatOperand(instruction, 0)
        };
    }

    private string GetRegisterPhrase(Register register, OpAccess access, ArgumentRegisterUse arguments)
    {
        var name = formatter.Format(register);

        var slot = RegisterHelper.GetArgumentSlot(register);

        if (slot < 0 || (AnnotatedSlots & (1 << slot)) != 0)
        {
            return name;
        }

        if (RegisterHelper.IsRead(access) && arguments.IsRead(slot))
        {
            AnnotatedSlots |= 1 << slot;

            return $"{name} (the {Ordinals[slot]} argument)";
        }

        if (RegisterHelper.IsWrite(access) && arguments.IsWritten(slot))
        {
            AnnotatedSlots |= 1 << slot;

            return $"{name} (the {Ordinals[slot]} argument of the next call)";
        }

        return name;
    }

    private string GetMemoryPhrase(in Instruction instruction, ArgumentRegisterUse arguments)
    {
        var size = instruction.MemorySize.GetSize();

        var location = GetAddressPhrase(instruction, arguments);

        return size switch
        {
            0 => $"the memory at {location}",
            1 => $"the byte at {location}",
            _ => $"the {size} bytes at {location}"
        };
    }

    private string GetAddressPhrase(in Instruction instruction, ArgumentRegisterUse arguments)
    {
        var expression = GetAddressExpression(instruction);

        if (instruction.SegmentPrefix == Register.GS)
        {
            return $"{expression} ({ThreadDataPhrase})";
        }

        var baseRegister = instruction.MemoryBase;

        var slot = RegisterHelper.GetArgumentSlot(baseRegister);

        if (slot < 0 || (AnnotatedSlots & (1 << slot)) != 0 || !arguments.IsRead(slot))
        {
            return expression;
        }

        AnnotatedSlots |= 1 << slot;

        return $"{expression} ({formatter.Format(baseRegister)} is the {Ordinals[slot]} argument)";
    }

    private string GetAddressExpression(in Instruction instruction)
    {
        Expression.Clear();

        if (instruction.SegmentPrefix != Register.None)
        {
            Expression.Append(formatter.Format(instruction.SegmentPrefix)).Append(':');
        }

        if (instruction.IsIPRelativeMemoryOperand)
        {
            return Expression.Append(formatter.FormatUInt64(instruction.IPRelativeMemoryAddress)).ToString();
        }

        var hasRegister = false;

        if (instruction.MemoryBase != Register.None)
        {
            Expression.Append(formatter.Format(instruction.MemoryBase));

            hasRegister = true;
        }

        if (instruction.MemoryIndex != Register.None)
        {
            if (hasRegister)
            {
                Expression.Append('+');
            }

            Expression.Append(formatter.Format(instruction.MemoryIndex));

            if (instruction.MemoryIndexScale > 1)
            {
                Expression.Append('*').Append(instruction.MemoryIndexScale);
            }

            hasRegister = true;
        }

        var displacement = (long)instruction.MemoryDisplacement64;

        if (!hasRegister)
        {
            Expression.Append(formatter.FormatUInt64(instruction.MemoryDisplacement64));
        }
        else if (displacement > 0)
        {
            Expression.Append('+').Append(formatter.FormatUInt64((ulong)displacement));
        }
        else if (displacement < 0)
        {
            Expression.Append('-').Append(formatter.FormatUInt64(0 - (ulong)displacement));
        }

        return Expression.ToString();
    }

    private string FormatOperand(in Instruction instruction, int operand)
    {
        var index = formatter.GetFormatterOperand(instruction, operand);

        if (index < 0)
        {
            return string.Empty;
        }

        formatter.FormatOperand(instruction, Output, index);

        return Output.ToStringAndReset();
    }

    private void AppendRegisterUse(in Instruction instruction, in InstructionInfo info)
    {
        ReadRegisters.Clear();

        WrittenRegisters.Clear();

        foreach (var used in info.GetUsedRegisters())
        {
            if (used.Register.IsSegmentRegister())
            {
                continue;
            }

            if (RegisterHelper.IsRead(used.Access) && !ReadRegisters.Contains(used.Register))
            {
                ReadRegisters.Add(used.Register);
            }

            if (RegisterHelper.IsWrite(used.Access) && !WrittenRegisters.Contains(used.Register))
            {
                WrittenRegisters.Add(used.Register);
            }
        }

        ReadRegisters.Sort();

        WrittenRegisters.Sort();

        formatter.FormatMnemonic(instruction, Output, FormatMnemonicOptions.NoPrefixes);

        Text.Append("Runs the ").Append(Output.ToStringAndReset()).Append(" instruction");

        if (ReadRegisters.Count > 0)
        {
            Text.Append(", which reads ");

            AppendRegisterList(ReadRegisters);
        }

        if (WrittenRegisters.Count > 0)
        {
            Text.Append(ReadRegisters.Count > 0 ? ", and writes " : ", which writes ");

            AppendRegisterList(WrittenRegisters);
        }

        Text.Append('.');
    }

    private void AppendRegisterList(List<Register> registers)
    {
        for (var index = 0; index < registers.Count; index++)
        {
            if (index > 0)
            {
                Text.Append(index == registers.Count - 1 ? " and " : ", ");
            }

            Text.Append(formatter.Format(registers[index]));
        }
    }
}
