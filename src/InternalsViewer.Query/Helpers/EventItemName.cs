using System.Collections.Concurrent;
using System.Reflection;

namespace InternalsViewer.Query.Helpers;

public static class EventItemName
{
    private static readonly ConcurrentDictionary<MemberInfo, string> Cache = new();

    public static string Get(Enum value)
    {
        var member = value.GetType().GetField(value.ToString())!;

        return Get(member);
    }

    public static string Get(Type type)
    {
        return Get((MemberInfo)type);
    }

    private static string Get(MemberInfo member)
    {
        return Cache.GetOrAdd(member, 
            m => m.GetCustomAttribute<EventItemNameAttribute>()?.Name ?? m.Name);
    }
}