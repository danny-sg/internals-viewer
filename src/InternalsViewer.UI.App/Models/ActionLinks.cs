using System.Collections.Generic;
using System.Linq;

namespace InternalsViewer.UI.App.Models;

public sealed record ActionLinks(IReadOnlyList<ActionLink> Links)
{
    public override string ToString() => string.Join("  ", Links.Select(l => l.Text));
}
