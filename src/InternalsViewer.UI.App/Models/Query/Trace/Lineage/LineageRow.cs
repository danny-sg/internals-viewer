namespace InternalsViewer.UI.App.Models.Query.Trace.Lineage;

public sealed class LineageRow(string name, int offset, string detail, bool hasPointer, bool isIndex, bool isTerminal)
{
    public string Name { get; } = name;

    public int Offset { get; } = offset;

    public string Detail { get; } = detail;

    public bool HasPointer { get; } = hasPointer;

    public bool IsIndex { get; } = isIndex;

    public bool IsTerminal { get; } = isTerminal;

    public LineageBox? Target { get; set; }

    public int TargetOffset { get; set; } = -1;

    public InternalsViewer.Query.FullTrace.Locators.SqlTypeInfo? ValueType { get; set; }

    public ulong Pointer { get; set; }

    public string PointerText { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
