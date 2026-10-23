using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Helpers;

internal static class TracePositionText
{
    public static string GetRulerText(TracePosition position, ulong originSequence)
        => position.IsKnown ? $"{(long)position.Sequence - (long)originSequence}:{position.Steps}" : "None";

    public static string GetHexText(TracePosition position) => position.IsKnown ? $"{position.Sequence:X}:{position.Steps:X}" : "None";
}
