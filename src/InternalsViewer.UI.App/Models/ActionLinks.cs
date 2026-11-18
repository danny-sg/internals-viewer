using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Models;

public sealed record ActionLinks(IReadOnlyList<ActionLink> Links)
{
    public string? Prefix { get; init; }

    public string? Suffix { get; init; }

    public bool IsChained { get; init; }

    public IReadOnlyList<string> Badges { get; init; } = [];

    public IReadOnlyList<ActionLink> Targets { get; init; } = [];

    public IReadOnlyList<ObjectBadge> ObjectBadges { get; init; } = [];

    public override string ToString()
    {
        var parts = Links.Select(l => l.Text).Concat(Badges);

        parts = Prefix is null ? parts : parts.Prepend(Prefix);

        parts = Targets.Count == 0 ? parts : parts.Append(PointerArrayText.Get([.. Targets.Select(t => t.Text)]));

        parts = Suffix is null ? parts : parts.Append(Suffix);

        return string.Join("  ", parts.Concat(ObjectBadges.Select(b => b.Name)));
    }
}
