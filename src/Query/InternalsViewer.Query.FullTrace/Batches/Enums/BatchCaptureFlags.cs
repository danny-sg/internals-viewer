namespace InternalsViewer.Query.FullTrace.Batches.Enums;

[Flags]
public enum BatchCaptureFlags : byte
{
    None = 0,
    Unreadable = 1,
    UnreadableSelection = 2,
    ImplicitSelection = 4
}
