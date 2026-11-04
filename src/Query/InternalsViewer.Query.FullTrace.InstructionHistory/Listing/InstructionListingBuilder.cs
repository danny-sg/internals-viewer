using Iced.Intel;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing.Explanations;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

public sealed class InstructionListingBuilder : IInstructionListingBuilder
{
    private const int Bitness = 64;

    private const int OperandColumn = 8;

    private const string MissingCodeText = "??";

    public Task<InstructionListing> BuildAsync(InstructionRequest request,
                                               TraceInstructionHistory history,
                                               CancellationToken cancellationToken)
        => Task.Run(() => Build(history, cancellationToken), cancellationToken);

    private static InstructionListing Build(TraceInstructionHistory history, CancellationToken cancellationToken)
    {
        var addresses = history.GetAddresses();

        var instructions = Decode(history, addresses, cancellationToken);

        var executionOrder = GetExecutionOrder(history, addresses, cancellationToken);

        var counts = GetExecutionCounts(executionOrder, addresses.Count);

        var arguments = ArgumentRegisterResolver.Resolve(instructions, executionOrder, cancellationToken);

        var formatter = CreateFormatter();

        var output = new StringOutput();

        var explanations = new InstructionExplanationBuilder(formatter);

        var countWidth = counts.Length == 0 ? 0 : counts.Max().ToString("N0").Length;

        var lines = new List<InstructionLine>(addresses.Count);

        for (var index = 0; index < addresses.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = GetText(formatter, output, instructions[index], history.GetCode(addresses[index]));

            var count = counts[index].ToString("N0").PadLeft(countWidth);

            var explanation = explanations.GetExplanation(instructions[index], arguments[index]);

            lines.Add(new InstructionLine(addresses[index], $"{count}  {text}", explanation));
        }

        return new InstructionListing(lines);
    }

    private static DecodedInstruction[] Decode(TraceInstructionHistory history,
                                               List<ulong> addresses,
                                               CancellationToken cancellationToken)
    {
        var reader = new InstructionCodeReader();

        var decoder = Decoder.Create(Bitness, reader, DecoderOptions.None);

        var instructions = new DecodedInstruction[addresses.Count];

        for (var index = 0; index < addresses.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var code = history.GetCode(addresses[index]);

            reader.Reset(code);

            decoder.IP = addresses[index];

            decoder.Decode(out var instruction);

            instructions[index] = new DecodedInstruction(instruction, decoder.LastError, code.Length);
        }

        return instructions;
    }

    private static int[] GetExecutionOrder(TraceInstructionHistory history,
                                           List<ulong> addresses,
                                           CancellationToken cancellationToken)
    {
        var positions = new Dictionary<ulong, int>(addresses.Count);

        for (var index = 0; index < addresses.Count; index++)
        {
            positions[addresses[index]] = index;
        }

        var steps = history.Steps;

        var order = new int[steps.Count];

        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            order[index] = positions.TryGetValue(steps[index].ProgramCounter, out var position) ? position : -1;
        }

        return order;
    }

    private static int[] GetExecutionCounts(int[] executionOrder, int addressCount)
    {
        var counts = new int[addressCount];

        foreach (var index in executionOrder)
        {
            if (index >= 0)
            {
                counts[index]++;
            }
        }

        return counts;
    }

    private static string GetText(Formatter formatter,
                                  StringOutput output,
                                  in DecodedInstruction decoded,
                                  ReadOnlySpan<byte> code)
    {
        if (decoded.Error == DecoderError.None)
        {
            formatter.Format(decoded.Instruction, output);

            return output.ToStringAndReset();
        }

        if (code.IsEmpty)
        {
            return MissingCodeText;
        }

        var length = decoded.Error == DecoderError.InvalidInstruction ? 1 : code.Length;

        return "db " + string.Join(", ", code[..length].ToArray().Select(b => $"0x{b:X2}"));
    }

    private static IntelFormatter CreateFormatter()
    {
        var formatter = new IntelFormatter();

        formatter.Options.HexPrefix = "0x";
        formatter.Options.HexSuffix = null;
        formatter.Options.FirstOperandCharIndex = OperandColumn;
        formatter.Options.SpaceAfterOperandSeparator = true;
        formatter.Options.BranchLeadingZeros = false;
        formatter.Options.ShowBranchSize = false;

        return formatter;
    }
}
