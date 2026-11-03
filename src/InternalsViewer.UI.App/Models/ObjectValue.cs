using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.UI.App.Models;

public sealed record ObjectValue(string? ClassName, string Text)
{
    public AddressMapEntry? Origin { get; init; }

    public bool IsLocal { get; init; }

    public override string ToString() => ClassName is null ? Text : $"{ClassName} {Text}";
}
