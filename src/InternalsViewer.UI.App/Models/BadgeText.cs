using System.Collections.Generic;
using System.Linq;

namespace InternalsViewer.UI.App.Models;

public sealed record BadgeText(IReadOnlyList<string> LeadingBadges, string Text, IReadOnlyList<string> Badges, string? Detail = null)
{
    public override string ToString()
    {
        var parts = LeadingBadges.Append(Text).Concat(Badges);

        return string.Join("  ", Detail is null ? parts : parts.Append(Detail));
    }
}
