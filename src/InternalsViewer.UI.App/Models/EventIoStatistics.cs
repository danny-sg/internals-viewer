namespace InternalsViewer.UI.App.Models;

public sealed record EventIoStatistics(long LogicalReads, long PhysicalReads, long ReadAheads);
