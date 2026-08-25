using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;
using InternalsViewer.UI.App.Services.Markers;

namespace InternalsViewer.UI.App.ViewModels.Columnstore.Dictionary;

public sealed partial class DictionaryTabViewModel
{
    [ObservableProperty]
    private int _selectedTabIndex;

    private const int EntriesTabIndex = 3;

    [ObservableProperty]
    private bool _isEntriesTabLoaded;

    [ObservableProperty]
    private int _selectedPageTabIndex;

    /// <summary>
    /// Hex Region
    /// </summary>
    [ObservableProperty]
    private DictionaryRegion _region = DictionaryRegion.Header;

    /// <summary>
    /// Move tab if region changes
    /// </summary>
    [ObservableProperty]
    private bool _isAutoRegion = true;

    /// <summary>
    /// Set while the region is being brought into line with the window, so it does not move the window in turn
    /// </summary>
    private bool _isFollowingWindow;

    /// <summary>
    /// Set while the window is being moved to a region, the region being the cause rather than something to follow
    /// </summary>
    private bool _isJumpingToRegion;

    partial void OnRegionChanged(DictionaryRegion value)
    {
        if (_isFollowingWindow)
        {
            return;
        }

        Hex.SelectedMarker = null;

        GoToRegion(value);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == EntriesTabIndex)
        {
            IsEntriesTabLoaded = true;
        }

        if (_isFollowingWindow)
        {
            return;
        }

        Hex.SelectedMarker = null;

        var region = GetTabRegion(value);

        if (region == Region)
        {
            GoToRegion(region);

            return;
        }

        Region = region;
    }

    private static DictionaryRegion GetTabRegion(int index) => index switch
    {
        1 => DictionaryRegion.Handles,
        2 => DictionaryRegion.Pages,
        3 => DictionaryRegion.Values,
        _ => DictionaryRegion.Header
    };

    private static int GetRegionTabIndex(DictionaryRegion region) => region switch
    {
        DictionaryRegion.Handles => 1,
        DictionaryRegion.Pages => 2,
        DictionaryRegion.Values => 3,
        _ => 0
    };

    private void GoToRegion(DictionaryRegion region)
    {
        if (Blob is not { } blob)
        {
            return;
        }

        _isJumpingToRegion = true;

        try
        {
            Hex.GoToOffset(DictionaryRegions.GetOffset(blob, region) / BlobHexViewModel.BytesPerLine
                           * BlobHexViewModel.BytesPerLine);
        }
        finally
        {
            _isJumpingToRegion = false;
        }
    }

    private void OnWindowMoved(object? sender, int start)
    {
        if (Blob is not { } blob || !IsAutoRegion || _isJumpingToRegion)
        {
            return;
        }

        var region = DictionaryRegions.GetRegion(blob, start);

        if (region == Region)
        {
            return;
        }

        _isFollowingWindow = true;

        Region = region;

        SelectedTabIndex = GetRegionTabIndex(region);

        _isFollowingWindow = false;

        Hex.BuildMarkers();
    }

    public IReadOnlyList<HexArea> HexAreas
    {
        get
        {
            switch (Blob)
            {
                case NumericDictionary:
                    return
                    [
                        new HexArea("Dictionary Header", 0),
                        new HexArea("Hash Table", 0x0C),
                        new HexArea("Array Header", 0x2C),
                        new HexArea("Values", NumericDictionary.HeaderSize)
                    ];

                case StringDictionary strings:
                    var pageSizes = StringDictionary.HandleArrayOffset + (strings.HandleCount * strings.HandleSize);

                    return
                    [
                        new HexArea("Dictionary Header", 0),
                        new HexArea("String Store", 0x0C),
                        new HexArea("Handle Array Header", StringDictionary.HandleArrayHeaderOffset),
                        new HexArea("Page Size Array Header", StringDictionary.PageSizeArrayHeaderOffset),
                        new HexArea("Handles", StringDictionary.HandleArrayOffset),
                        new HexArea("Page Sizes", pageSizes),
                        new HexArea("Pages", pageSizes + (strings.PageCount * DictionaryMarkerBuilder.PageSizeBytes))
                    ];

                default:
                    return [];
            }
        }
    }
}
