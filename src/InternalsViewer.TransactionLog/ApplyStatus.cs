namespace InternalsViewer.TransactionLog;

public enum ApplyStatus
{
    Applied,
    PageMismatch,
    LsnMismatch,
    BeforeImageMismatch,
    NotSupported
}
