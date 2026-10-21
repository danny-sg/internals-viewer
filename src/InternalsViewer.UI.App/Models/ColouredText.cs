using System.Drawing;

namespace InternalsViewer.UI.App.Models;

public sealed record ColouredText(string Text, Color Colour)
{
    public override string ToString() => Text;
}
