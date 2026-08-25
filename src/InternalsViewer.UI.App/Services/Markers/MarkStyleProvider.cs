using System.Collections.Generic;
using System.Diagnostics;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Services.Markers;

public sealed class MarkStyleProvider
{
    private readonly Dictionary<ItemType, MarkStyle> _styleCache = [];

    public MarkStyleProvider()
    {
        Initialize();
    }

    public static MarkStyleProvider Default { get; } = new();

    private ResourceDictionary? ThemeDictionary { get; set; }

    public MarkStyle GetDefaultMarkStyle()
    {
        object? resource = null;

        ThemeDictionary?.TryGetValue("DefaultMarkerStyle", out resource);

        var style = resource as MarkStyle ?? new MarkStyle();

        return style;
    }

    public MarkStyle GetMarkStyle(ItemType itemType)
    {
        if (_styleCache.TryGetValue(itemType, out var cached))
        {
            return cached;
        }

        object? resource = null;

        ThemeDictionary?.TryGetValue($"{itemType}MarkerStyle", out resource);

        if(Debugger.IsAttached && resource == null)
        {
            // Debugger.Break();
        }

        if (resource == null)
        {
            ThemeDictionary?.TryGetValue("DefaultMarkerStyle", out resource);
        }

        var style = resource as MarkStyle ?? new MarkStyle();

        _styleCache[itemType] = style;

        return style;
    }

    private void Initialize()
    {
        var currentTheme = Application.Current.RequestedTheme;

        ThemeDictionary = Application.Current.Resources.ThemeDictionaries[currentTheme.ToString()] as ResourceDictionary;
    }
}