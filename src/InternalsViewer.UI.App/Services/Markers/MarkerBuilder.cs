using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Services.Markers;

/// <summary>
/// Builds Markers for use in the Hex Viewer
/// </summary>
public static class MarkerBuilder
{
    public static List<Marker> BuildMarkers(DataStructure markedObject)
    {
        var styleProvider = new MarkStyleProvider();

        var markers = new List<Marker>();

        foreach (var item in markedObject.MarkItems)
        {
            switch (item)
            {
                case PropertyItem propertyItem:
                    markers.Add(BuildPropertyMarker(propertyItem, markedObject, styleProvider));
                    break;
                case ValueItem valueItem:
                    markers.Add(BuildValueMarker(valueItem, styleProvider));
                    break;
            }
        }

        return markers.OrderBy(o => o.Ordinal).ThenBy(o => o.StartPosition).ToList();
    }

    private static Marker BuildValueMarker(ValueItem item, MarkStyleProvider styleProvider)
    {
        var marker = new Marker();

        SetMarkerPosition(item, marker);

        var style = styleProvider.GetMarkStyle(item.ItemType);

        SetStyle(marker, style);

        marker.Name = item.Name;

        marker.HasKey = item.Offset >= 0;

        marker.Tags = item.Tags;

        SetValue(marker, item.Value);

        return marker;
    }

    private static Marker BuildPropertyMarker(PropertyItem item,
                                              DataStructure markedObject,
                                              MarkStyleProvider styleProvider)
    {
        var marker = new Marker();

        SetMarkerPosition(item, marker);

        var property = markedObject.GetType().GetProperty(item.PropertyName);

        if (property == null)
        {
            marker.Name = item.PropertyName.SplitCamelCase();

            return marker;
        }

        var value = property.GetValue(markedObject, null);

        SetValue(marker, value);

        MarkStyle? style;

        var markAttribute = property?.GetCustomAttributes(typeof(DataStructureItemAttribute), false)?.FirstOrDefault();

        // Check if the property has a DataStructureItemAttribute, that will have information about the marker style
        if (markAttribute is DataStructureItemAttribute attribute)
        {
            style = styleProvider.GetMarkStyle(attribute.ItemType);

            marker.Type = attribute.ItemType;

            marker.Ordinal = style.Ordinal;

            marker.Name = string.IsNullOrEmpty(attribute.Name) ? style.Name : attribute.Name;

            marker.IsVisible = attribute.IsVisible;
        }
        else
        {
            style = styleProvider.GetMarkStyle(item.ItemType);

            marker.Name = item.PropertyName.SplitCamelCase();
        }

        marker.Tags = item.Tags;

        marker.HasKey = item.Offset >= 0;

        SetStyle(marker, style);

        return marker;
    }

    private static void SetStyle(Marker marker, MarkStyle style)
    {
        marker.ForeColour = style.ForeColour.Color;
        marker.BackColour = style.BackColour.Color;
        marker.AlternateBackColour = style.AlternateBackColour.Color;
    }

    /// <summary>
    /// Sets the marker position.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="marker">The marker.</param>
    private static void SetMarkerPosition(DataStructureItem item, Marker marker)
    {
        marker.StartPosition = item.Offset;

        if (item.Length > 0)
        {
            marker.EndPosition = item.Offset + item.Length - 1;
        }
        else
        {
            marker.EndPosition = item.Offset;
            marker.IsNull = true;
        }
    }

    /// <summary>
    /// Sets the value for a marker, including recursively adding markers on marked properties
    /// </summary>
    private static void SetValue(Marker marker, object? value)
    {
        if (value is DataStructure markedObject)
        {
            marker.Children = BuildMarkers(markedObject).ToObservableCollection();
        }

        if (value is DataStructure[] markedObjectArray)
        {
            marker.Children = markedObjectArray.SelectMany(BuildMarkers).ToObservableCollection();
        }

        try
        {
            switch (value, marker.Children.Any())
            {
                case (RecordField field, _):
                    marker.Value = field.Value;
                    break;
                case (byte[] bytes, _):
                    marker.Value = "0x" + bytes.ToHexString();
                    break;
                case (BitArray bitArray, _):
                    marker.Value = StringHelpers.GetBitArrayString(bitArray);
                    break;
                case (short[] shortArray, _):
                    marker.Value = StringHelpers.GetArrayString(shortArray);
                    break;
                case (ushort[] ushortArray, _):
                    marker.Value = StringHelpers.GetArrayString(ushortArray);
                    break;
                case (byte byteValue, _):
                    marker.Value = $"{byteValue} (0x{byteValue:X})";
                    break;
                case (DataStructure, true):
                case (DataStructure[], true):
                    marker.Value = string.Empty;
                    break;
                default:
                    {
                        marker.Value = value?.ToString() ?? string.Empty;

                        if (value is PageAddress or RowIdentifier)
                        {
                            marker.MarkerType = MarkerType.PageAddress;
                        }

                        break;
                    }
            }

        }
        catch (Exception ex)
        {
            marker.Value = $"Error - {ex.Message}";
        }


    }
}

[Flags]
public enum MarkerType
{
    Undefined,
    PageAddress,
    VariableLengthField,
    FixedLengthField,
    Uniqueifier,
    Flag,
    Bitmap,
    Internal,
    Lob,
    Compressed,
    None
}