namespace InternalsViewer.Query.Events.Waits;

public static class WaitTypeExtensions
{
    public static bool IsPageLatchWait(this WaitType waitType)
    {
        return waitType switch
        {
            WaitType.PAGELATCH_NL => true,
            WaitType.PAGELATCH_KP => true,
            WaitType.PAGELATCH_SH => true,
            WaitType.PAGELATCH_UP => true,
            WaitType.PAGELATCH_EX => true,
            WaitType.PAGELATCH_DT => true,

            _ => false
        };
    }

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