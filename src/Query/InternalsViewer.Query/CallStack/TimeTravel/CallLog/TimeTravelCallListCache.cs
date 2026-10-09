namespace InternalsViewer.Query.CallStack.TimeTravel.CallLog;

internal sealed class TimeTravelCallListCache(TimeTravelCallLog log)
{
    private Dictionary<ulong, TimeTravelCallList?> Lists { get; } = [];

    public TimeTravelCallList? Of(ulong address)
    {
        if (!Lists.TryGetValue(address, out var list))
        {
            list = log.CallsOf(address, 0);

            Lists[address] = list;
        }

        return list;
    }
}
