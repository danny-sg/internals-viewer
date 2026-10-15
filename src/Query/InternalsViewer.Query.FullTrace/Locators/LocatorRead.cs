using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorRead(ulong Function,
                                            int Register,
                                            bool IsReadAtReturn,
                                            LocatorValueType ValueType,
                                            int Offset = 0,
                                            int[]? Path = null)
{
    public uint Mode => ValueType switch
    {
        LocatorValueType.Argument => 1,
        LocatorValueType.Text => 2,
        LocatorValueType.WideText => 3,
        LocatorValueType.TypeInfo => 4,
        _ => 0
    };
}
