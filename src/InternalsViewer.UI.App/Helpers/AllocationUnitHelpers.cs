using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.UI.App.Helpers;

internal static class AllocationUnitHelpers
{
    public static string DisplayName(this AllocationUnit unit)
        => string.IsNullOrEmpty(unit.IndexName) ? unit.TableName ?? string.Empty : unit.IndexName;
}
