using System.Drawing;

namespace InternalsViewer.Internals.Engine.Address;

public sealed record PageSpan(PageAddress Address, long Start, long End)
{
    public PageSpan(PageAddress address, long start, long end, Color displayColour) 
        : this(address, start, end)
    {
        DisplayColour = displayColour;
    }

    public Color? DisplayColour { get; set; }
}
