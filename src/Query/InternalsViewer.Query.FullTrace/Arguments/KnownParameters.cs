namespace InternalsViewer.Query.FullTrace.Arguments;

public static class KnownParameters
{
    private const string HoBtIdName = "hobt_id";

    private static readonly (string ClassName, string MethodName, string Type)[] HoBtIds =
    [
        ("HoBtAccess", "Init", "unsigned __int64")
    ];

    public static ArgumentLayout Apply(ArgumentLayout layout, string? className, string? methodName)
        => ApplyHoBtIds(ApplyAccessorFlags(layout), className, methodName);

    private static ArgumentLayout ApplyAccessorFlags(ArgumentLayout layout)
    {
        var slots = layout.Slots;

        for (var index = 0; index < slots.Count; index++)
        {
            if (BindingArguments.IsBindings(slots[index]) && BindingArguments.GetFlagsIndex(slots, index) is var flags and >= 0)
            {
                var retyped = slots.Select((s, i) => i == flags ? s with { Type = BindingArguments.AccessorFlagsType } : s);

                return layout with { Slots = [.. retyped] };
            }
        }

        return layout;
    }

    private static ArgumentLayout ApplyHoBtIds(ArgumentLayout layout, string? className, string? methodName)
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
