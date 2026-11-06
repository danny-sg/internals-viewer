namespace InternalsViewer.Query.Events.Waits;

internal static class WaitTypeExtensions
{
    public static bool IsPageIoLatchWait(this WaitType waitType)
    {
        return waitType switch
        {
            WaitType.PAGEIOLATCH_NL => true,
            WaitType.PAGEIOLATCH_KP => true,
            WaitType.PAGEIOLATCH_SH => true,
            WaitType.PAGEIOLATCH_UP => true,
            WaitType.PAGEIOLATCH_EX => true,
            WaitType.PAGEIOLATCH_DT => true,

            _ => false
        };
    }
}