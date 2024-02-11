using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Services.Markers;
using Windows.UI;

namespace InternalsViewer.UI.App.Models;

public class Marker: DependencyObject
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

    public bool IsVisible { get; set; } = true;
        
    public MarkerType MarkerType { get; set; }
        
    public ItemType Type { get; set; }

    public int? Ordinal { get; set; }

    public List<string> Tags { get; set; } = new();

    public static Marker? GetMarkerAtPosition(int startPosition, int endPosition, List<Marker> markers)
    {
        return markers.FirstOrDefault(marker => marker.StartPosition >= startPosition & marker.EndPosition <= endPosition);
    }

    public ObservableCollection<Marker> Children { get; set; } = new();

    public bool HasKey { get; set; }
}