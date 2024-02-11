using CommunityToolkit.WinUI.Helpers;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Models;
using System.Diagnostics;

namespace InternalsViewer.UI.App.Services.Markers;

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

    public MarkStyle GetDefaultMarkStyle()
    {
        object? resource = null;

        ThemeDictionary?.TryGetValue("DefaultMarkerStyle", out resource);

        var style = resource as MarkStyle ?? new MarkStyle();

        return style;
    }

    public MarkStyle GetMarkStyle(ItemType itemType)
    {
        object? resource = null;

        ThemeDictionary?.TryGetValue($"{itemType}MarkerStyle", out resource);

        if(Debugger.IsAttached && resource == null)
        {
            Debugger.Break();
        }

        if (resource == null)
        {
            ThemeDictionary?.TryGetValue("DefaultMarkerStyle", out resource);
        }

        var style = resource as MarkStyle ?? new MarkStyle();

        return style;
    }
}