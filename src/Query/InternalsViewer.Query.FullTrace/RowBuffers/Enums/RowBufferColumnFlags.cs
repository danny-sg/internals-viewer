namespace InternalsViewer.Query.FullTrace.RowBuffers.Enums;

[Flags]
public enum RowBufferColumnFlags : byte
{
    None = 0,
    ByReference = 1,
    HasLength = 2,
    Truncated = 4,
    Unreadable = 8,
    Deferred = 16,
    BindingUnreadable = 32,
    AlternateBuffer = 64,
    SlotUnreadable = 128
}
