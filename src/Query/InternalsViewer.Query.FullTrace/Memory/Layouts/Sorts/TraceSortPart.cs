using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;

public readonly record struct TraceSortPart(TraceSortPartType Type, ulong Address);
