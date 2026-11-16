using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public static class SortTablePartLabel
{
    public static string GetName(TraceSortPartType type) => type switch
    {
        TraceSortPartType.Iterator => "Sort Iterator",
        TraceSortPartType.Rowset => "Rowset",
        TraceSortPartType.Descriptor => "Sort Descriptor",
        TraceSortPartType.RecordPage => "Record Page",
        _ => "Compare Page"
    };
}
