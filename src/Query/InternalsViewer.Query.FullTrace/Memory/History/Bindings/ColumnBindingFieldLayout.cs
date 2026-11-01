using InternalsViewer.Query.FullTrace.Memory.History.Bindings.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History.Bindings;

public sealed record ColumnBindingFieldLayout(ColumnBindingField Field, int Offset, int Length, bool IsInfrequent = false);
