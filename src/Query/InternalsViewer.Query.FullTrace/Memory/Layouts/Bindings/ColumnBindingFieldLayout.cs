using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;

public sealed record ColumnBindingFieldLayout(ColumnBindingField Field, int Offset, int Length, bool IsInfrequent = false);
