using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Arguments;

internal static class BindingArguments
{
    public const string AccessorFlagsType = FunctionSignature.EnumPrefix + "DBACCESSORFLAGS";

    private const string CountType = "unsigned __int64";

    private const string FlagsType = "unsigned long";

    private const string ExtendedFlagsType = "AccessorFlagEx";

    private const string ExtendedFlagsEnumType = FunctionSignature.EnumPrefix + ExtendedFlagsType;

    public static bool IsBindings(ArgumentSlot slot) => LocatorType.GetLocatorType(slot.Type) is { ValueType: LocatorValueType.Bindings };

    public static int GetCountIndex(IReadOnlyList<ArgumentSlot> slots, int bindings)
        => bindings > 0 && slots[bindings - 1] is { Type: CountType, Location: ArgumentLocation.Register or ArgumentLocation.Stack }
            ? bindings - 1
            : -1;

    public static int GetFlagsIndex(IReadOnlyList<ArgumentSlot> slots, int bindings)
    {
        var flags = GetCountIndex(slots, bindings) - 1;

        if (flags >= 0 && slots[flags].Type is ExtendedFlagsType or ExtendedFlagsEnumType)
        {
            flags--;
        }

        return flags >= 0 && slots[flags].Type == FlagsType ? flags : -1;
    }
}
