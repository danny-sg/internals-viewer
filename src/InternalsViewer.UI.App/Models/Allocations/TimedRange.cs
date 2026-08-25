namespace InternalsViewer.UI.App.Models.Allocations;

public readonly record struct TimedRange(int FromCell, int ToCell, long StartUs, long EndUs);