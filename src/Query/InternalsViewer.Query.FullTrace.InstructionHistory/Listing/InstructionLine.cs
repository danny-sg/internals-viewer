namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

public sealed record InstructionLine(ulong Address, string Text, string? Explanation);
