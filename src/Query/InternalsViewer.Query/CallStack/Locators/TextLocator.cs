namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct TextLocator(Locator Locator)
{
    public string Text => Locator.Text ?? string.Empty;

    public bool IsTruncated => Locator.Detail != 0;

    public static TextLocator? From(Locator locator)
        => locator.ValueType is LocatorValueType.Text or LocatorValueType.WideText ? new TextLocator(locator) : null;

    public static Locator Create(int register, LocatorValueType valueType, string text, bool isTruncated)
        => new(register, valueType, (ulong)text.Length, isTruncated ? 1ul : 0, text);

    public override string ToString() => IsTruncated ? $"{Text} (Truncated)" : Text;
}
