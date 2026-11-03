namespace InternalsViewer.Query.FullTrace.Arguments;

public static class KnownParameters
{
    private const string HoBtIdName = "hobt_id";

    private const string HoBtIdType = "unsigned __int64";

    private const string ThisType = "this";

    private static readonly (string? ClassName, string MethodName, int Parameter)[] HoBtIds =
    [
        ("HoBtAccess", "Init", 2),
        (null, "GetHoBtLockInternal", 2),
        (null, "CreateAccessorCommon", 2),
        ("DatasetSession", "Create", 1),
        ("IndexDataSetSession", "WakeUpInternal", 1),
        ("HeapDataSetSession", "WakeUpInternal", 1),
        ("OpenRowsetSS", "OpenRowset", 1),
        ("RowsetNewSS", "Init", 3),
        ("RowsetNewSS", "WakeUpInternal", 2),
        ("RecoveryUnit", "IsPVSRowsetId", 1),
        ("RecoveryUnit", "IsRegularPVSRowsetId", 1),
        ("RecoveryUnit", "IsLongTermPVSRowsetId", 1)
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
        if (HoBtIds.FirstOrDefault(h => h.ClassName == className && h.MethodName == methodName) is not { Parameter: > 0 } known)
        {
            return layout;
        }

        var slots = layout.Slots.ToArray();

        var parameter = 0;

        for (var index = 0; index < slots.Length; index++)
        {
            if (slots[index].Type == ThisType || slots[index].Name == ArgumentLayout.ReturnBuffer || ++parameter != known.Parameter)
            {
                continue;
            }

            if (slots[index] is { Type: HoBtIdType, Name.Length: 0 })
            {
                slots[index] = slots[index] with { Name = HoBtIdName, IsHoBtId = true };
            }

            break;
        }

        return layout with { Slots = slots };
    }
}
