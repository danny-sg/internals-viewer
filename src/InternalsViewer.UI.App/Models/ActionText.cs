using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models;

public sealed record ActionText(string Text, IReadOnlyList<ActionLink> Actions)
{
    public override string ToString() => Text;
}
