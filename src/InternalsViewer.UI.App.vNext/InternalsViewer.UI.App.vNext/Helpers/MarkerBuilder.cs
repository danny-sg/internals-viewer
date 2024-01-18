using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.UI.App.vNext.Models;

namespace InternalsViewer.UI.App.vNext.Helpers;


public class MarkStyle
{
    public MarkStyle(Color foreColour, Color backColour, Color alternateBackColour)
    {
        ForeColour = foreColour;
        BackColour = backColour;
        AlternateBackColour = alternateBackColour;
    }

    public MarkStyle(Color foreColour, Color backColour)
    {
        ForeColour = foreColour;
        BackColour = backColour;
        AlternateBackColour = backColour;
    }

    public MarkStyle(Color foreColour, Color backColour, string description)
    {
        ForeColour = foreColour;
        BackColour = backColour;
        AlternateBackColour = backColour;

        Description = description;
    }

    public MarkStyle(Color foreColour, Color backColour, Color alternateBackColour, string description)
    {
        ForeColour = foreColour;
        BackColour = backColour;
        AlternateBackColour = alternateBackColour;
        Description = description;
    }

    /// <summary>
    /// Gets or sets the mark display description.
    /// </summary>
    /// <value>The description.</value>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fore colour.
    /// </summary>
    /// <value>The fore colour.</value>
    public Color ForeColour { get; set; }

    /// <summary>
    /// Gets or sets the back colour.
    /// </summary>
    /// <value>The back colour.</value>
    public Color BackColour { get; set; }

    /// <summary>
    /// Gets or sets the alternate back colour.
    /// </summary>
    /// <value>The alternate back colour.</value>
    public Color AlternateBackColour { get; set; }
}

public class MarkStyleProvider
{
    public Dictionary<DataStructureItemType, MarkStyle> Styles = new()
    {
        { DataStructureItemType.Rid, new MarkStyle(Color.DarkMagenta, Color.Thistle, "Row Identifier")},
        { DataStructureItemType.Uniqueifier, new MarkStyle(Color.SteelBlue, Color.AliceBlue, "Uniqueifier")},

        { DataStructureItemType.BlobChildOffset, new MarkStyle(Color.Blue, Color.Thistle, "Offset")},
        { DataStructureItemType.BlobChildLength, new MarkStyle(Color.Red, Color.Thistle, "Length")},
        { DataStructureItemType.StatusBitsA, new MarkStyle(Color.Red, Color.Gainsboro, "Status Bits A")},
        { DataStructureItemType.StatusBitsB, new MarkStyle(Color.Maroon, Color.Gainsboro, "Status Bits B")},
        { DataStructureItemType.BlobId, new MarkStyle(Color.Navy, Color.AliceBlue, "ID")},
        { DataStructureItemType.BlobLength, new MarkStyle(Color.Blue, Color.Gainsboro, "Length")},
        { DataStructureItemType.BlobType, new MarkStyle(Color.DarkGreen, Color.Gainsboro, "Type")},
        { DataStructureItemType.MaxLinks, new MarkStyle(Color.Firebrick, Color.Gainsboro, "MaxLinks")},
        { DataStructureItemType.CurrentLinks, new MarkStyle(Color.Firebrick, Color.Gainsboro, "Current Links")},
        { DataStructureItemType.Level, new MarkStyle(Color.SlateGray, Color.Gainsboro, "Level")},
        { DataStructureItemType.BlobSize, new MarkStyle(Color.Purple, Color.Gainsboro, "Size")},
        { DataStructureItemType.BlobData, new MarkStyle(Color.Gray, Color.PaleGoldenrod,  "Data")},
        { DataStructureItemType.CompressedValue, new MarkStyle(Color.Black, Color.PaleGreen, Color.LightGreen ,"Data")},
        { DataStructureItemType.ForwardingStub, new MarkStyle(Color.DarkBlue, Color.Gainsboro,  "Forwarding Stub")},
        { DataStructureItemType.DownPagePointer, new MarkStyle(Color.White, Color.Navy,  "Down Page Pointer")},
        { DataStructureItemType.ColumnOffsetArray, new MarkStyle(Color.Blue, Color.AliceBlue,  "Column Offset Array")},
        { DataStructureItemType.ColumnCount, new MarkStyle(Color.DarkGreen, Color.Gainsboro,  "Column Count")},
        { DataStructureItemType.ColumnCountOffset, new MarkStyle(Color.Blue, Color.Gainsboro,  "Column Count Offset")},
        { DataStructureItemType.SlotOffset, new MarkStyle(Color.Black, Color.White,  "Slot Offset")},
        { DataStructureItemType.VariableLengthColumnCount, new MarkStyle(Color.Black, Color.AliceBlue,  "Variable Length Column Count")},
        { DataStructureItemType.NullBitmap, new MarkStyle(Color.Purple, Color.Gainsboro,  "Null Bitmap")},
        { DataStructureItemType.Value, new MarkStyle(Color.Gray, Color.LemonChiffon, Color.PaleGoldenrod, string.Empty)},
        { DataStructureItemType.SparseColumns, new MarkStyle(Color.Black, Color.Olive, "Sparse Columns")},
        { DataStructureItemType.SparseColumnOffsets, new MarkStyle(Color.Black, Color.DarkKhaki, "Sparse Column Offsets")},
        { DataStructureItemType.SparseColumnCount, new MarkStyle(Color.Black, Color.SeaGreen, "Sparse Column Count")},
        { DataStructureItemType.ComplexHeader, new MarkStyle(Color.Green, Color.Gainsboro, "Complex Header")},
        { DataStructureItemType.PageModCount, new MarkStyle(Color.DarkGreen, Color.Gainsboro, "Page Mod Count")},
        { DataStructureItemType.CiSize, new MarkStyle(Color.Purple, Color.Gainsboro, "Size")},
        { DataStructureItemType.CiLength, new MarkStyle(Color.Blue, Color.Gainsboro, "Length")},
        { DataStructureItemType.Timestamp, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Timestamp")},
        { DataStructureItemType.PointerType, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Type")},
        { DataStructureItemType.EntryCount, new MarkStyle(Color.AliceBlue, Color.Gainsboro, "Entry Count")},
        { DataStructureItemType.OverflowLength, new MarkStyle(Color.Red, Color.PeachPuff, "Length")},
        { DataStructureItemType.Unused, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Unused")},
        { DataStructureItemType.UpdateSeq, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "UpdateSeq")},
        { DataStructureItemType.CdArrayItem, new MarkStyle(Color.White, Color.Orange,string.Empty)},
        { DataStructureItemType.SlotCount, new MarkStyle(Color.DarkGreen, Color.PeachPuff,"Slot Count")},
    };

    public MarkStyle GetMarkStyle(DataStructureItemType dataStructureItemType)
    {
        return Styles[dataStructureItemType];
    }
}

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
        var styleProvider = new MarkStyleProvider();

        var markers = new List<Marker>();

        var index = 0;

        foreach (var item in markedObject.MarkItems.OrderBy(o => o.StartPosition))
        {
            var marker = new Marker();

            SetMarkerPosition(item, marker);

            var property = markedObject.GetType().GetProperty(item.PropertyName);

            var markAttribute = property?.GetCustomAttributes(typeof(DataStructureItemAttribute), false);

            if (markAttribute is { Length: > 0 })
            {
                if (markAttribute[0] is not DataStructureItemAttribute attribute)
                {
                    continue;
                }

                var style = styleProvider.GetMarkStyle(attribute.DataStructureItemType);

                marker.ForeColour = style.ForeColour;

                if (style.BackColour != style.AlternateBackColour && index % 2 == 0)
                {
                    marker.BackColour = style.AlternateBackColour;
                }
                else
                {
                    marker.BackColour = style.BackColour;
                }

                marker.Name = style.Description;

                marker.IsVisible = attribute.IsVisible;

                if (string.IsNullOrEmpty(prefix) | string.IsNullOrEmpty(style.Description))
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

            if (property == null)
            {
                continue;
            }

            // Check if there is an index, if there is it indicates the property is an array
            if (item.Index < 0)
            {
                var value = property.GetValue(markedObject, null) ?? string.Empty;

                SetValue(markers, marker, value, prefix + item.Prefix);
            }
            else
            {
                var array = (object[])property.GetValue(markedObject, null)!;

                string value;

                if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(item.Prefix))
                {
                    value = $"{prefix}{item.Prefix}";
                }
                else
                {
                    value = $"{prefix} - {item.Prefix}";
                }

                SetValue(markers, marker, array[item.Index], value);
            }

            if (item.StartPosition > 0)
            {
                index += 1;
                markers.Add(marker);
            }
        }

        return markers;
    }

    /// <summary>
    /// Sets the marker position.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="marker">The marker.</param>
    private static void SetMarkerPosition(DataStructureItem item, Marker marker)
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
        switch (value)
        {
            case DataStructure dataStructure:
                markers.AddRange(BuildMarkers(dataStructure, prefix));
                break;
            case byte[] bytes:
                marker.Value = DataConverter.BinaryToString(bytes, SqlDbType.VarChar);
                break;
            default:
                {
                    marker.Value = value.ToString() ?? string.Empty;

                    if (value is PageAddress or RowIdentifier)
                    {
                        marker.MarkerType = MarkerType.PageAddress;
                    }

                    break;
                }
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
    Compressed
}