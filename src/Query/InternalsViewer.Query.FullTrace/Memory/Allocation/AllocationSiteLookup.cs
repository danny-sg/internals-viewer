using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

/// <summary>
/// Resolves function arguments to a file/line (allocation site)
/// </summary>
internal static class AllocationSiteLookup
{
    private const string LineType = "int";

    internal static (int File, int Line)? GetSiteSlots(ArgumentLayout layout)
    {
        var slots = layout.Slots;

        for (var index = 0; index + 1 < slots.Count; index++)
        {
            var file = slots[index];

            var line = slots[index + 1];

            if (TraceArgumentCall.IsCaptured(file)
                && TraceArgumentCall.IsCaptured(line)
                && LocatorType.GetLocatorType(file.Type) is { ValueType: LocatorValueType.Text }
                && line.Type == LineType)
            {
                return (file.Index, line.Index);
            }
        }

        return null;
    }
}
