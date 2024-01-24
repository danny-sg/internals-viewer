using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CommunityToolkit.WinUI.Helpers;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.UI.App.Models;
using Microsoft.UI;

namespace InternalsViewer.UI.App.Helpers;

public class MarkStyleProvider
{
    private ResourceDictionary? ThemeDictionary { get; set; }

    public MarkStyleProvider()
    {
        Initialize();
    }

    private void Initialize()
    {
        var themeListener = new ThemeListener();

        var currentTheme = themeListener.CurrentTheme;

        ThemeDictionary = Application.Current.Resources.ThemeDictionaries[currentTheme.ToString()] as ResourceDictionary;
    }

    public MarkStyle GetMarkStyle(DataStructureItemType dataStructureItemType)
    {
        object? resource = null;

        ThemeDictionary?.TryGetValue($"MarkerStyle{dataStructureItemType}", out resource);

        var style = resource as MarkStyle ?? new MarkStyle();   

        return style;
    }
}

/// <summary>
/// Builds Markers for use in the Hex Viewer
/// </summary>
public static class MarkerBuilder
{
    public static List<Marker> BuildMarkers(DataStructure markedObject)
    {
        return BuildMarkers(markedObject, string.Empty);
    }

    /// <summary>
    /// Builds the markers using an IMarkable collection and reflection to access the property values
    /// </summary>
    private static List<Marker> BuildMarkers(DataStructure markedObject, string prefix)
    {
        var styleProvider = new MarkStyleProvider();

        var markers = new List<Marker>();

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

                marker.ItemType = attribute.DataStructureItemType;

                marker.ForeColour = style.ForeColour.Color;

                marker.BackColour = style.BackColour.Color;
                marker.AlternateBackColour = style.AlternateBackColour.Color;

                var name = string.IsNullOrEmpty(attribute.Description) ? style.Name : attribute.Description;

                marker.IsVisible = attribute.IsVisible;

                if (string.IsNullOrEmpty(prefix) | string.IsNullOrEmpty(style.Name))
                {
                    marker.Name = prefix + name;
                }
                else
                {
                    marker.Name = prefix + " - " + name;
                }
            }
            else
            {
                marker.Name = item.PropertyName;
                marker.ForeColour = Colors.Black;
                marker.BackColour = Colors.Transparent;
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

            if (item.StartPosition >= 0)
            {
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