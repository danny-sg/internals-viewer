namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ArgumentRow(string name,
                                 string type,
                                 string source,
                                 string value,
                                 ulong? raw = null,
                                 IteratorTarget? iterator = null)
{
    public string Name { get; } = name;

    public string Type { get; } = type;

    public string Source { get; } = source;

    public string Value { get; } = value;

    public ulong? Raw { get; } = raw;

    public IteratorTarget? Iterator { get; } = iterator;

    public string? IteratorLabel => Iterator?.Label;

    public bool CanFind => Raw is not null;
}
