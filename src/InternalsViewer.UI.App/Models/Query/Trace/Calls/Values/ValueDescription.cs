using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Values;

public sealed record ValueDescription(string Text, string? ClassName = null)
{
    public AddressMapEntry? Origin { get; init; }

    public bool IsLocal { get; init; }

    public static implicit operator ValueDescription(string text) => new(text);
}
