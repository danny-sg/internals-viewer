using System.Drawing;

namespace InternalsViewer.Internals.Engine.Address;

public sealed record PageSpan(PageAddress Address, long StartUs, long EndUs)
{
    public PageSpan(PageAddress address, long startUs, long endUs, Color displayColour) 
        : this(address, startUs, endUs)
    {
        DisplayColour = displayColour;
    }

    public Color? DisplayColour { get; set; }
}
