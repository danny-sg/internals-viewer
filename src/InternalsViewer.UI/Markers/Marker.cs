﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace InternalsViewer.UI.Markers;

public class Marker
{
    public static Marker GetMarkerAtPosition(int startPosition, int endPosition, List<Marker> markers)
    {
        return markers.First(marker => marker.StartPosition >= startPosition & marker.EndPosition <= endPosition);
    }

    public override string ToString()
    {
        return string.Format("{0}: Start:{1} End:{2} Back Colour:{3} Fore Colour:{4}",
            Name,
            StartPosition,
            EndPosition,
            BackColour.Name,
            ForeColour.Name);
    }

    public string Name { get; set; } = string.Empty;

    public int StartPosition { get; set; }

    public int EndPosition { get; set; }

    public Color BackColour { get; set; }

    public Color AlternateBackColour { get; set; }

    public Color ForeColour { get; set; }

    public string Value { get; set; } = string.Empty;

    public MarkerType DataType { get; set; }

    public bool IsNull { get; set; }

    public bool Visible { get; set; }
}