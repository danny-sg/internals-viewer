using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Annotations;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Services.Markers;

/// <summary>
/// Builds Markers for use in the Hex Viewer
/// </summary>
public static class MarkerBuilder
{
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), MarkedProperty?> PropertyCache = new();

    public static List<Marker> BuildMarkers(IDataStructure markedObject)
    {
        return BuildMarkers(markedObject, MarkStyleProvider.Default);
    }

    /// <summary>
    /// Builds a marker for something generated rather than declared, applying the style its item type carries
    /// </summary>
    /// <remarks>
    /// The array regions of a columnstore blob hold far too many entries to mark as they are parsed, so their
    /// markers are built for the window on screen instead of coming from a data structure's mark items.
    /// </remarks>
    public static Marker CreateMarker(string name, ItemType type, int offset, int size, string value)
    {
        var style = MarkStyleProvider.Default.GetMarkStyle(type);

        var marker = new Marker
        {
            Name = name,
            Type = type,
            StartPosition = offset,
            EndPosition = offset + size - 1,
            Value = value,
            HasKey = true,
            Ordinal = style.Ordinal
        };

        SetStyle(marker, style);

        return marker;
    }

    private static List<Marker> BuildMarkers(IDataStructure markedObject, MarkStyleProvider styleProvider)
    {
        var items = markedObject.MarkItems;

        var markers = new List<Marker>(items.Count);

        foreach (var item in items)
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

        return [.. markers.OrderBy(o => o.Ordinal).ThenBy(o => o.StartPosition)];
    }

    /// <summary>
    /// Resolves the reflection metadata a <see cref="PropertyItem"/> marker needs
    /// </summary>
    /// <remarks>
    /// The property/attribute pair for a given data structure type never changes for the lifetime of the process, so it
    /// is resolved once and reused. Markers are rebuilt on every page, slot and theme change, and the same handful of
    /// types recur each time.
    /// </remarks>
    private static MarkedProperty? GetMarkedProperty(Type type, string propertyName)
    {
        return PropertyCache.GetOrAdd((type, propertyName), static key =>
        {
            var property = key.Type.GetProperty(key.PropertyName);

            if (property is null)
            {
                return null;
            }

            return new MarkedProperty(property, property.GetCustomAttribute<DataStructureItemAttribute>(false));
        });
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

        SetValue(marker, item.Value, styleProvider);

        return marker;
    }

    private static Marker BuildPropertyMarker(PropertyItem item,
                                              IDataStructure markedObject,
                                              MarkStyleProvider styleProvider)
    {
        var marker = new Marker();

        SetMarkerPosition(item, marker);

        var markedProperty = GetMarkedProperty(markedObject.GetType(), item.PropertyName);

        if (markedProperty is null)
        {
            marker.Name = item.PropertyName.SplitCamelCase();

            return marker;
        }

        var value = markedProperty.Property.GetValue(markedObject, null);

        SetValue(marker, value, styleProvider);

        MarkStyle? style;

        // Check if the property has a DataStructureItemAttribute, that will have information about the marker style
        if (markedProperty.Attribute is { } attribute)
        {
            style = styleProvider.GetMarkStyle(attribute.ItemType);

            marker.Type = attribute.ItemType;

            marker.Ordinal = style.Ordinal;

            var styleName = string.IsNullOrEmpty(style.Name) ? null : style.Name;

            marker.Name = string.IsNullOrEmpty(attribute.Name) ? styleName ?? item.PropertyName.SplitCamelCase() : attribute.Name;

            marker.IsVisible = attribute.IsVisible;
        }
        else
        {
            style = styleProvider.GetMarkStyle(item.ItemType);

            marker.IsVisible = item.IsVisible;

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

    private static void SetMarkerPosition(DataStructureItem item, Marker marker)
    {
        marker.StartPosition = item.Offset;
        marker.BitOffset = item.BitOffset;
        marker.BitLength = item.BitLength;

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
    private static void SetValue(Marker marker, object? value, MarkStyleProvider styleProvider)
    {
        var hasChildren = false;

        if (value is DataStructure markedObject)
        {
            var children = BuildMarkers(markedObject, styleProvider);

            marker.Children = children.ToObservableCollection();

            hasChildren = children.Count > 0;
        }
        else if (value is DataStructure[] markedObjectArray)
        {
            var children = new List<Marker>();

            foreach (var child in markedObjectArray)
            {
                children.AddRange(BuildMarkers(child, styleProvider));
            }

            marker.Children = children.ToObservableCollection();

            hasChildren = children.Count > 0;
        }

        try
        {
            switch (value, hasChildren)
            {
                case (RecordField field, _):
                    marker.Value = field.Value;
                    break;
                case (byte[] bytes, _):
                    marker.Value = "0x" + bytes.ToHexString();
                    break;
                case (ReadOnlyMemory<byte> memory, _):
                    marker.Value = "0x" + memory.ToArray().ToHexString();
                    break;
                case (BitArray bitArray, _):
                    marker.Value = StringHelpers.GetBitArrayString(bitArray);
                    break;
                case (int[] intArray, _):
                    marker.Value = StringHelpers.GetArrayString(intArray);
                    break;
                case (short[] shortArray, _):
                    marker.Value = StringHelpers.GetArrayString(shortArray);
                    break;
                case (ushort[] ushortArray, _):
                    marker.Value = StringHelpers.GetArrayString(ushortArray);
                    break;
                case (ColumnDescriptor[] columnDescriptors, _):
                    marker.Value = StringHelpers.GetArrayString(columnDescriptors);
                    break;
                case (Enum enumValue, _):
                    marker.Value = enumValue.ToString().SplitCamelCase();
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

    /// <summary>
    /// A marked property and its style attribute, if it has one
    /// </summary>
    private sealed record MarkedProperty(PropertyInfo Property, DataStructureItemAttribute? Attribute);
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