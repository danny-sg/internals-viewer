using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed record ArgumentSlot(string Name, string Type, ArgumentLocation Location, int Index, string Source, bool IsHoBtId = false);
