using System;
using System.Drawing;

namespace InternalsViewer.UI;

public class OffsetEventArgs(ushort offset, string markerDescription, Color foreColour, Color backColour)
    : EventArgs
{
    public Color ForeColour { get; set; } = foreColour;

    public Color BackColour { get; set; } = backColour;

    public string MarkerDescription { get; set; } = markerDescription;

    public ushort Offset { get; set; } = offset;
}