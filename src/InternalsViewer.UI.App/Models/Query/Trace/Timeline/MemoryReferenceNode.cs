using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemoryReferenceNode(MemoryHistoryPointer pointer,
                                        string target,
                                        bool isCycle,
                                        bool canOpen,
                                        MemoryReferenceNode? parent)
{
    private const string CycleSuffix = "  (Cycle)";

    public MemoryHistoryPointer Pointer { get; } = pointer;

    public string Target { get; } = target;

    public bool IsCycle { get; } = isCycle;

    public bool CanOpen { get; } = canOpen;

    public MemoryReferenceNode? Parent { get; } = parent;

    public string OffsetText => $"[+0x{Pointer.Offset:X2}]";

    public string TargetText => IsCycle ? Target + CycleSuffix : Target;

    public string ValueText => $"0x{Pointer.Value:X16}";

    public bool CanExpand => Pointer.Block >= 0;
}
