using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public static class HashTablePartLabel
{
    public static string GetName(TraceHashPartType type) => type switch
    {
        TraceHashPartType.Group => "Partition Group",
        TraceHashPartType.Partition => "Partition",
        TraceHashPartType.Directory => "Directory",
        TraceHashPartType.PointerPage => "Pointer Page",
        TraceHashPartType.BucketPage => "Item Page",
        TraceHashPartType.OverflowPage => "Overflow Page",
        TraceHashPartType.WorkfilePage => "Workfile Page",
        TraceHashPartType.WorkfileRowset => "Workfile Rowset",
        TraceHashPartType.Bitmap => "Bitmap",
        _ => "Bitmap Page"
    };

    public static string GetGroupName(int slot, bool isWorktable)
    {
        if (isWorktable)
        {
            return "Worktable Group";
        }

        return slot % TraceHashTableLayout.GroupsPerSide == 0 ? "Partition Group" : "Rebuild Group";
    }

    public static string GetSideName(int slot) => slot / TraceHashTableLayout.GroupsPerSide == 0 ? InputLabel.Build : InputLabel.Probe;

    public static string GetText(TraceHashPart part) => part.Type switch
    {
        TraceHashPartType.Group => GetName(part.Type),
        TraceHashPartType.Partition => $"Partition {part.Partition}",
        _ => $"Partition {part.Partition} {GetName(part.Type)}"
    };
}
