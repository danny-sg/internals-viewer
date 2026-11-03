namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;

[Flags]
public enum ColumnBindingFlags : uint
{
    None = 0,
    Length = 0x100,
    ByReference = 0x800,
    LargeObject = 0x4000
}
