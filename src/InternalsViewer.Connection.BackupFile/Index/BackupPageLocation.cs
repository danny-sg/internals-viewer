namespace InternalsViewer.Connection.BackupFile.Index;

/// <summary>
/// File (via stripe index) + offset address
/// </summary>
internal readonly record struct BackupPageLocation(int StripeIndex, long Offset);
