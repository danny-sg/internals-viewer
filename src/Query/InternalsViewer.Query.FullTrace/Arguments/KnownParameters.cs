namespace InternalsViewer.Query.FullTrace.Arguments;

public static class KnownParameters
{
    private const string HoBtIdName = "hobt_id";

    private static readonly (string ClassName, string MethodName, string Type)[] HoBtIds =
    [
        ("HoBtAccess", "Init", "unsigned __int64")
    ];

    public static ArgumentLayout Apply(ArgumentLayout layout, string? className, string? methodName)
    {
        if (!HoBtIds.Any(h => h.ClassName == className && h.MethodName == methodName))
        {
            return layout;
        }

        var slots = layout.Slots.Select(s => IsHoBtId(className, methodName, s) ? s with { Name = HoBtIdName, IsHoBtId = true } : s);

        return layout with { Slots = [.. slots] };
    }

    private static bool IsHoBtId(string? className, string? methodName, ArgumentSlot slot)
        => slot.Name.Length == 0 && HoBtIds.Any(h => h.ClassName == className && h.MethodName == methodName && h.Type == slot.Type);
}
