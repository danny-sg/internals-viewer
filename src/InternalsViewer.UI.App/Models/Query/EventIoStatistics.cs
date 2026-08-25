namespace InternalsViewer.UI.App.Models.Query;

public sealed record EventIoStatistics(long LogicalReads, long PhysicalReads, long ReadAheads);
