using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.Services.Diagnostics;

namespace InternalsViewer.UI.App.ViewModels.Columnstore.Segment;

public sealed partial class SegmentTabViewModel
{
    private const int DataTabIndex = 5;

    /// <summary>
    /// Set while the region is being brought into line with the window, so it does not move the window in turn
    /// </summary>
    private bool _isFollowingWindow;

    /// <summary>
    /// Set while the window is being moved to a region, the region being the cause rather than something to follow
    /// </summary>
    private bool _isJumpingToRegion;

    private IReadOnlyList<HexArea>? _hexAreas;

    [ObservableProperty]
    private int _selectedRegionTabIndex;

    [ObservableProperty]
    private bool _isDataTabLoaded;

    [ObservableProperty]
    private int _selectedVariableLengthDataTabIndex;

    /// <summary>
    /// Region the window sits on, set by picking a tab and reported back when a scroll leaves the region
    /// </summary>
    [ObservableProperty]
    private SegmentRegion _region = SegmentRegion.Header;

    /// <summary>
    /// Whether scrolling out of a region moves on to the tab for the region scrolled into
    /// </summary>
    [ObservableProperty]
    private bool _isAutoRegion = true;

    /// <summary>
    /// The blob's regions in the order they sit on disk, which the hex gutter names while a drag is under way
    /// </summary>
    public IReadOnlyList<HexArea> HexAreas => _hexAreas ??= BuildHexAreas();

    public void GoToTarget(SegmentNavigationTarget target)
    {
        if (Blob is null)
        {
            return;
        }

        _isFollowingWindow = true;

        Region = target.Region;

        _isFollowingWindow = false;

        SelectedRegionTabIndex = GetRegionTabIndex(target.Region);

        Hex.GoToOffset(target.Offset);

        Hex.SelectedMarker = Hex.Markers.FirstOrDefault(m => m.StartPosition == target.Offset - Hex.HexBaseAddress);
    }

    partial void OnSelectedRegionTabIndexChanged(int value)
    {
        if (value == DataTabIndex)
        {
            IsDataTabLoaded = true;
        }

        Hex.SelectedMarker = null;

        Hex.BuildMarkers();
    }

    /// <summary>
    /// A marker belongs to the tab it was picked on, so moving between them leaves nothing selected
    /// </summary>
    partial void OnSelectedVariableLengthDataTabIndexChanged(int value)
    {
        Hex.SelectedMarker = null;

        Hex.BuildMarkers();
    }

    partial void OnRegionChanged(SegmentRegion value)
    {
        if (_isFollowingWindow)
        {
            return;
        }

        // A marker belongs to the region it was built for, so it means nothing once another region is on show
        Hex.SelectedMarker = null;

        GoToRegion(value);
    }

    /// <summary>
    /// Tab a region is shown on, so following something into another region brings its tab forward
    /// </summary>
    private static int GetRegionTabIndex(SegmentRegion region) => region switch
    {
        SegmentRegion.Bookmarks => 1,
        SegmentRegion.RleArray => 2,
        SegmentRegion.BitpackArray => 3,
        SegmentRegion.VariableLengthData => 4,
        _ => 0
    };

    /// <summary>
    /// Moves the window to the region's first line, or rebuilds in place if it is already there
    /// </summary>
    private void GoToRegion(SegmentRegion region)
    {
        using var timing = Logger.Time("Go to region", $"{region}");

        _isJumpingToRegion = true;

        try
        {
            var offset = GetRegionOffset(region);

            Hex.GoToOffset(offset);
        }
        finally
        {
            _isJumpingToRegion = false;
        }
    }

    private int GetRegionOffset(SegmentRegion region)
        => Blob is not { } blob ? 0 : SegmentRegions.GetOffset(blob, region) / 16 * 16;

    /// <summary>
    /// Brings the region into line with the window, so a scroll past a boundary moves on to the tab it landed in
    /// </summary>
    /// <remarks>
    /// A jump is skipped because the region is what moved the window, and following it back would only fight the
    /// line alignment - a region starting part way into a line resolves to the region before it.
    /// </remarks>
    private void OnWindowMoved(object? sender, int start)
    {
        if (Blob is not { } blob || !IsAutoRegion || _isJumpingToRegion)
        {
            return;
        }

        var region = SegmentRegions.GetRegion(blob, start);

        if (region == Region)
        {
            return;
        }

        _isFollowingWindow = true;

        Region = region;

        _isFollowingWindow = false;

        Hex.BuildMarkers();
    }

    private IReadOnlyList<HexArea> BuildHexAreas()
    {
        if (Blob is not { } blob)
        {
            return [];
        }

        var header = blob.Header;

        List<HexArea> areas =
        [
            new("Header", 0),
            new("Bookmarks", header.BookmarkArrayOffset),
            new("RLE Array", header.RleArrayOffset)
        ];

        if (header.IsVariableLengthData)
        {
            areas.Add(new HexArea("VLD", header.VariableLengthDataOffset));
        }
        else if (header.HasBitpackArray)
        {
            areas.Add(new HexArea("Bit Pack", header.BitpackArrayOffset));
        }

        return [.. areas.OrderBy(a => a.Start)];
    }
}
