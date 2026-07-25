namespace InternalsViewer.Connection.BackupFile.Mapping;

/// <summary>
/// File (via stripe index) + offset address
/// </summary>
internal readonly record struct PageLocation(int StripeIndex, long Offset);
