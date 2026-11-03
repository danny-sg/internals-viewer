using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;

public sealed record ColumnBinding(int Index,
                                   int Ordinal,
                                   int RecordOffset,
                                   int ValueOffset,
                                   int StatusOffset,
                                   int? LengthOffset,
                                   int MaximumLength,
                                   byte Type,
                                   ColumnBindingFlags Flags,
                                   ulong ColumnAttributes,
                                   ulong InfrequentData)
{
    public bool IsVariableLength => RecordOffset < 0;

    public int VariableIndex => IsVariableLength ? -RecordOffset : 0;

    public bool IsByReference => (Flags & ColumnBindingFlags.ByReference) != 0;

    public int ValueLength => IsByReference ? sizeof(ulong) : MaximumLength;
}
