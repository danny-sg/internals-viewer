﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace InternalsViewer.UI.App.vNext.Models
{
    public record Marker
    {
        public string Name { get; set; } = string.Empty;

        public int StartPosition { get; set; }

        public int EndPosition { get; set; }

        public int Length => EndPosition - StartPosition;   

        public Color BackColour { get; set; }

        public Color AlternateBackColour { get; set; }

        public Color ForeColour { get; set; }

        public string Value { get; set; } = string.Empty;

        public bool IsNull { get; set; }

        public bool IsVisible { get; set; }

        public static Marker? GetMarkerAtPosition(int startPosition, int endPosition, List<Marker> markers)
        {
            return markers.FirstOrDefault(marker => marker.StartPosition >= startPosition & marker.EndPosition <= endPosition);
        }
    }
}