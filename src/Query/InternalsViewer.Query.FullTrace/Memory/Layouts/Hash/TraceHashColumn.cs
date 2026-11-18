using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public sealed record TraceHashColumn(ColumnBinding Binding, ulong Value, ulong ScanValue, ulong Accessor, ulong Bindings);
