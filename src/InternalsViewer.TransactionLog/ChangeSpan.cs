namespace InternalsViewer.Query.TransactionLog;

/// <summary>
/// Byte range of a page image changed by applying a log record
/// </summary>
public sealed record ChangeSpan(int Offset, int Length, string Description);
