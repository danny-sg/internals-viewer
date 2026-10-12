namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed record ArgumentSlot(string Name, string Type, ArgumentLocation Location, int Index, string Source);
