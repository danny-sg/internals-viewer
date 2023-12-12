using System.Collections.Generic;
using System.Data;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Records;
using InternalsViewer.UI.MarkStyles;

namespace InternalsViewer.UI.Markers;

/// <summary>
/// Builds Markers for use in the Hex Viewer
/// </summary>
public class MarkerBuilder
{
    public static List<Marker> BuildMarkers(DataStructure markedObject)
    {
        return BuildMarkers(markedObject, string.Empty);
    }

    /// <summary>
    /// Builds the markers using an IMarkable collection and reflection to access the property values
    /// </summary>
    public static List<Marker> BuildMarkers(DataStructure markedObject, string prefix)
    {
        if (markedObject == null)
        {
            return new List<Marker>();
        }

        var styleProvider = new MarkStyleProvider();

        var markers = new List<Marker>();

        foreach (var item in markedObject.MarkItems)
        {
            var marker = new Marker();

            SetMarkerPosition(item, marker);

            var property = markedObject.GetType().GetProperty(item.PropertyName);

            var markAttribute = property?.GetCustomAttributes(typeof(DataStructureItemAttribute), false);

            if (markAttribute != null && markAttribute.Length > 0)
            {
                var attribute = (markAttribute[0] as DataStructureItemAttribute);
                    
                var style = styleProvider.GetMarkStyle(attribute.DataStructureItemType);
                    
                marker.ForeColour = style.ForeColour;
                marker.BackColour = style.BackColour;
                marker.AlternateBackColour = style.AlternateBackColour;
                marker.Name = style.Description;

                marker.Visible = attribute.Visible;

                if (string.IsNullOrEmpty(prefix) | string.IsNullOrEmpty(attribute?.Description))
                {

                    marker.Name = prefix + style.Description;
                }
                else
                {
                    marker.Name = prefix + " - " + style.Description;
                }
            }
            else
            {
                marker.Name = item.PropertyName;
            }

            // Check if there is an index, if there is it indicates the property is an array
            if (item.Index < 0)
            {
                var value = property.GetValue(markedObject, null);

                SetValue(markers, marker, value, prefix + item.Prefix);
            }
            else
            {
                var array = (object[])property.GetValue(markedObject, null);

                SetValue(markers, marker, array[item.Index], prefix + item.Prefix);
            }

            if (item.StartPosition > 0)
            {
                markers.Add(marker);
            }
        }
        // Sort the markers in order of their positions
        markers.Sort((marker1, marker2) => marker1.StartPosition.CompareTo(marker2.StartPosition));

        return markers;
    }

    /// <summary>
    /// Sets the marker position.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="marker">The marker.</param>
    private static void SetMarkerPosition(MarkItem item, Marker marker)
    {
        marker.StartPosition = item.StartPosition;

        if (item.Length > 0)
        {
            marker.EndPosition = item.StartPosition + item.Length - 1;
        }
        else
        {
            marker.EndPosition = item.StartPosition;
            marker.IsNull = true;
        }
    }

    /// <summary>
    /// Sets the value for a marker, including recursively adding markers on marked properties
    /// </summary>
    private static void SetValue(List<Marker> markers, Marker marker, object value, string prefix)
    {
        var markable = value as DataStructure;

        if (markable != null)
        {
            markers.AddRange(BuildMarkers(markable, prefix));
        }
        else if (value is byte[])
        {
            marker.Value = DataConverter.BinaryToString((byte[])value, SqlDbType.VarChar, 0, 0);
        }
        else
        {
            marker.Value = value.ToString();

            if (value is PageAddress || value is RowIdentifier)
            {
                marker.DataType = MarkerType.PageAddress;
            }
        }
    }
}