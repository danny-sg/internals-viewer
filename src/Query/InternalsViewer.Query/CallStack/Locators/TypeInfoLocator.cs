namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct TypeInfoLocator(Locator Locator)
{
    public SqlTypeInfo TypeInfo => SqlTypeInfo.From(Locator.Value, Locator.Detail);

    public static TypeInfoLocator? From(Locator locator)
        => locator.ValueType == LocatorValueType.TypeInfo ? new TypeInfoLocator(locator) : null;

    public static Locator Create(int register, ulong header, ulong detail) => new(register, LocatorValueType.TypeInfo, header, detail);

    public override string ToString() => TypeInfo.ToString();
}
