using System.Collections.Generic;
using System.Linq;

namespace InternalsViewer.UI.App.Models;

public sealed record ActionLinks(IReadOnlyList<ActionLink> Links)
{
    public string? Suffix { get; init; }

    public override string ToString()
        => Suffix is null ? string.Join("  ", Links.Select(l => l.Text)) : $"{string.Join("  ", Links.Select(l => l.Text))}  {Suffix}";
}
