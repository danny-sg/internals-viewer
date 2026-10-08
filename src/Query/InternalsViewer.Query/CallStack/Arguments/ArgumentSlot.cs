namespace InternalsViewer.Query.CallStack.Arguments;

public sealed record ArgumentSlot(string Name, string Type, ArgumentLocation Location, int Index, string Source)
{
    public bool IsPrimitivePointer => Location is ArgumentLocation.Register or ArgumentLocation.Stack
                                      && ArgumentValue.IsPrimitivePointer(Type);
}
