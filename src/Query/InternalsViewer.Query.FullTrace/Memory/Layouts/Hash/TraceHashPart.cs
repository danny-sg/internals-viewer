using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public readonly record struct TraceHashPart(TraceHashPartType Type, int Partition, ulong Address);
