using System.Collections.Generic;
using System.Linq;

namespace InternalsViewer.UI.App.Models;

public sealed record ActionLinks(IReadOnlyList<ActionLink> Links)
{
    public string? Suffix { get; init; }

    public IReadOnlyList<string> Badges { get; init; } = [];

    public override string ToString()
    {
        var parts = Links.Select(l => l.Text).Concat(Badges);

        return string.Join("  ", Suffix is null ? parts : parts.Append(Suffix));
    }
}
