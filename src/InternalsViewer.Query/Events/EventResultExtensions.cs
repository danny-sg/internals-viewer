namespace InternalsViewer.Query.Events;

public static class EventResultExtensions
{
    public static short? GetShort(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && short.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static int? GetInt(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && int.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static long? GetLong(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && long.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static ulong? GetUlong(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && ulong.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static string GetString(this EventResult e, string key)
    {
        return e.Data.TryGetValue(key, out var val) ? val?.ToString() ?? string.Empty : string.Empty;
    }

    public static int GetDatabaseId(this EventResult e)
    {
        if (e.Actions.TryGetValue("database_id", out var val) && int.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return -1;
    }
}