using System.Collections.Generic;
using System.Reflection;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.UI.Markers
{
    /// <summary>
    /// Builds Markers for use in the Hex Viewer
    /// </summary>
    class MarkerBuilder
    {
        /// <summary>
        /// Builds the markers using an IMarkable collection and reflection to access the property values
        /// </summary>
        /// <param name="markedObject">The marked object.</param>
        /// <returns></returns>
        public static List<Marker> BuildMarkers(IMarkable markedObject)
        {
            bool alternate = false;

            List<Marker> markers = new List<Marker>();

            foreach (MarkItem item in markedObject.MarkItems)
            {
                Marker marker = new Marker();

                SetMarkerPosition(item, marker);

                PropertyInfo property = markedObject.GetType().GetProperty(item.PropertyName);

                object[] description = property.GetCustomAttributes(typeof(MarkAttribute), false);

                if (description != null && description.Length > 0)
                {
                    MarkAttribute attribute = (description[0] as MarkAttribute);

                    marker.Name = attribute.Description;
                    marker.ForeColour = attribute.ForeColour;

                    marker.BackColour = alternate ? attribute.AlternateBackColour : attribute.BackColour;
                    alternate = !alternate;

                    marker.Visible = attribute.Visible;
                }
                else
                {
                    marker.Name = item.PropertyName;
                }

                // Check if there is an index, if there is it indicates the property is an array
                if (item.Index < 0)
                {
                    object value = property.GetValue(markedObject, null);

                    SetValue(markers, marker, value);
                }
                else
                {
                    object[] array = (object[])property.GetValue(markedObject, null);

                    // Assume objects in the array always have a Name and Value property
                    PropertyInfo nameInfo = array[item.Index].GetType().GetProperty("Name");
                    PropertyInfo valueInfo = array[item.Index].GetType().GetProperty("Value");

                    marker.Name = nameInfo.GetValue(array[item.Index], null).ToString();
                    object value = valueInfo.GetValue(array[item.Index], null);

                    SetValue(markers, marker, value);
                }

                markers.Add(marker);
            }
            // Sort the markers in order of their positions
            markers.Sort(delegate(Marker marker1, Marker marker2) { return marker1.StartPosition.CompareTo(marker2.StartPosition); });

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
        /// <param name="markers">The markers.</param>
        /// <param name="marker">The marker.</param>
        /// <param name="value">The value.</param>
        private static void SetValue(List<Marker> markers, Marker marker, object value)
        {
            if (value is IMarkable)
            {
                markers.AddRange(BuildMarkers((IMarkable)value));
            }
            else
            {
                marker.Value = value.ToString();
            }
        }
    }
}
