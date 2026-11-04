using Iced.Intel;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

internal readonly record struct DecodedInstruction(Instruction Instruction, DecoderError Error, int CodeLength);
