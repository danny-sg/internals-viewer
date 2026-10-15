namespace InternalsViewer.Query.FullTrace.Batches;

[Flags]
public enum BatchVectorFlags : byte
{
    None = 0,
    Pure = 1,
    Unreadable = 2,
    Missing = 4,
    UnreadableSlots = 8
}
