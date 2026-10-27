using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.ViewModels.Columnstore.Dictionary;
using InternalsViewer.UI.App.ViewModels.Columnstore.Segment;
using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

public sealed partial class ColumnstoreTabViewModel : IMcpContextSource, IColumnstoreCommands
{
    private const int DocumentLoadTimeoutMs = 15000;

    private const int DocumentLoadPollMs = 50;

    private const int SegmentDataTabIndex = 5;

    private static readonly string[] SegmentRegionNames =
        ["Header", "Bookmarks", "RleArray", "BitpackArray", "VariableLengthData", "Data"];

    private static readonly string[] DictionaryRegionNames = ["Header", "Handles", "Pages", "Entries"];

    private DocumentViewModel? CurrentDocumentViewModel
        => Dock.Groups().FirstOrDefault(g => g.Documents.Contains(_structureDocument!))?.SelectedDocument;

    public TabContext GetContext()
    {
        var content = CurrentDocumentViewModel?.Content;

        var hex = content switch
        {
            SegmentTabViewModel segment => segment.Hex,
            DictionaryTabViewModel dictionary => dictionary.Hex,
            _ => null
        };

        var marker = hex?.SelectedMarker;

        return new ColumnstoreContext
        {
            TabId = TabId,
            Title = Name,
            Database = Database,
            AllocationUnit = AllocationUnit,
            Index = Index,
            Documents = [.. Dock.Groups().SelectMany(g => g.Documents).Select(d => d.Title)],
            CurrentDocument = CurrentDocumentViewModel?.Title,
            Region = content switch
            {
                SegmentTabViewModel segment => SegmentRegionNames[Math.Clamp(segment.SelectedRegionTabIndex, 0, SegmentDataTabIndex)],
                DictionaryTabViewModel dictionary => DictionaryRegionNames[Math.Clamp(dictionary.SelectedTabIndex, 0, 3)],
                _ => null
            },
            AvailableRegions = content switch
            {
                SegmentTabViewModel => SegmentRegionNames,
                DictionaryTabViewModel => DictionaryRegionNames,
                _ => []
            },
            MetadataTab = (ColumnstoreMetadataTab)Math.Clamp(SelectedMetadataTabIndex, 0, 1),
            MarkerNames = hex is null ? [] : [.. FlattenMarkers(hex.Markers).Select(m => m.Name).Where(n => n.Length > 0)],
            SelectedMarker = marker?.Name,
            SelectedMarkerValue = marker?.Value,
            SelectedRow = content switch
            {
                SegmentTabViewModel segment => segment.SelectedRow?.Ordinal,
                DictionaryTabViewModel { SelectedEntryIndex: >= 0 } dictionary => dictionary.SelectedEntryIndex,
                _ => null
            }
        };
    }

    public async Task<string> OpenAsync(ColumnstoreItem item, int? rowGroup, int? column, int? dictionary)
    {
        RequireInitialized();

        switch (item)
        {
            case ColumnstoreItem.Segment:
            {
                var segment = Segments.FirstOrDefault(s => s.RowGroupId == rowGroup && s.ColumnId == column)
                              ?? throw new InvalidOperationException(
                                  $"No segment for row group {rowGroup} column {column}. Both are required.");

                OpenSegment(segment);

                await WaitForDocumentAsync();

                return $"Opened {CurrentDocumentViewModel?.Title}.";
            }

            case ColumnstoreItem.Dictionary:
            {
                var match = Dictionaries
                    .Select(d => d.Dictionary)
                    .FirstOrDefault(d => d.ColumnId == column && (dictionary is null || d.DictionaryId == dictionary))
                    ?? throw new InvalidOperationException(
                        $"No dictionary for column {column}{(dictionary is null ? string.Empty : $" dictionary {dictionary}")}.");

                OpenDictionary(match);

                await WaitForDocumentAsync();

                return $"Opened {CurrentDocumentViewModel?.Title}.";
            }

            case ColumnstoreItem.DeltaStore:
            {
                var group = RowGroups.FirstOrDefault(r => r.RowGroupId == rowGroup)
                            ?? throw new InvalidOperationException($"No row group {rowGroup}.");

                OpenDeltaStore(group);

                return $"Opened {CurrentDocumentViewModel?.Title}.";
            }

            default:
                OpenDeleteBitmap();

                return $"Opened {CurrentDocumentViewModel?.Title}.";
        }
    }

    public Task<string> ShowDocumentAsync(string name)
    {
        var documents = Dock.Groups().SelectMany(g => g.Documents).ToList();

        var document = documents.FirstOrDefault(d => string.Equals(d.Title, name, StringComparison.OrdinalIgnoreCase))
                       ?? documents.FirstOrDefault(d => d.Title.Contains(name, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException(
                           $"No open document matches '{name}'. Open: {string.Join(", ", documents.Select(d => d.Title))}.");

        var group = Dock.Groups().First(g => g.Documents.Contains(document));

        group.SelectedDocument = document;

        Dock.NotifySelectionChanged();

        return Task.FromResult($"Showing {document.Title}.");
    }

    public Task<string> ShowMetadataTabAsync(ColumnstoreMetadataTab tab)
    {
        SelectedMetadataTabIndex = (int)tab;

        return Task.FromResult($"Showing the {tab} list.");
    }

    public async Task<string> ShowRegionAsync(string region)
    {
        var content = await RequireOpenDocumentAsync();

        switch (content)
        {
            case SegmentTabViewModel segment:
            {
                var index = Array.FindIndex(SegmentRegionNames, n => string.Equals(n, region, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                {
                    throw new InvalidOperationException($"No region '{region}'. Regions: {string.Join(", ", SegmentRegionNames)}.");
                }

                if (index < SegmentDataTabIndex)
                {
                    segment.Region = (SegmentRegion)index;
                }

                segment.SelectedRegionTabIndex = index;

                return $"Showing the {SegmentRegionNames[index]} region.";
            }

            case DictionaryTabViewModel dictionary:
            {
                var index = Array.FindIndex(DictionaryRegionNames, n => string.Equals(n, region, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                {
                    throw new InvalidOperationException($"No region '{region}'. Regions: {string.Join(", ", DictionaryRegionNames)}.");
                }

                dictionary.SelectedTabIndex = index;

                return $"Showing the {DictionaryRegionNames[index]} region.";
            }

            default:
                throw new InvalidOperationException("The current document has no regions.");
        }
    }

    public async Task<string> SelectMarkerAsync(string? name)
    {
        var hex = (await RequireOpenDocumentAsync()) switch
        {
            SegmentTabViewModel segment => segment.Hex,
            DictionaryTabViewModel dictionary => dictionary.Hex,
            _ => throw new InvalidOperationException("The current document has no hex view.")
        };

        if (string.IsNullOrWhiteSpace(name))
        {
            hex.SelectedMarker = null;

            return "Cleared the marker selection.";
        }

        var markers = FlattenMarkers(hex.Markers).ToList();

        var marker = markers.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                     ?? markers.FirstOrDefault(m => m.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException(
                         $"No marker matches '{name}'. Markers: {string.Join(", ", markers.Select(m => m.Name).Take(40))}.");

        hex.SelectedMarker = marker;

        return $"Selected {marker.Name} at offset {marker.StartPosition}, {marker.EndPosition - marker.StartPosition + 1} bytes.";
    }

    public async Task<string> SelectRowAsync(int? index)
    {
        switch (await RequireOpenDocumentAsync())
        {
            case SegmentTabViewModel segment:
            {
                if (index is null)
                {
                    segment.SelectRow(null);

                    return "Cleared the row selection.";
                }

                if (segment.Rows is not { } rows || index < 0 || index >= rows.Count)
                {
                    throw new InvalidOperationException($"Row {index} is outside the segment. It has {segment.Rows?.Count ?? 0} rows.");
                }

                segment.SelectedRegionTabIndex = SegmentDataTabIndex;

                segment.SelectRow(rows[index.Value]);

                return $"Selected row {index}.";
            }

            case DictionaryTabViewModel dictionary:
            {
                if (index is null)
                {
                    dictionary.SelectedEntryIndex = -1;

                    return "Cleared the entry selection.";
                }

                if (dictionary.Blob is not { } blob || index < 0 || index >= blob.EntryCount)
                {
                    throw new InvalidOperationException(
                        $"Entry {index} is outside the dictionary. It has {dictionary.Blob?.EntryCount ?? 0} entries.");
                }

                dictionary.SelectedTabIndex = DictionaryRegionNames.Length - 1;

                dictionary.SelectedEntryIndex = index.Value;

                return $"Selected entry {index}.";
            }

            default:
                throw new InvalidOperationException("The current document has no rows. Open a segment or dictionary first.");
        }
    }

    private static IEnumerable<Marker> FlattenMarkers(IEnumerable<Marker> markers)
    {
        foreach (var marker in markers)
        {
            yield return marker;

            foreach (var child in FlattenMarkers(marker.Children))
            {
                yield return child;
            }
        }
    }

    private void RequireInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("The columnstore index is still loading.");
        }
    }

    private async Task<object> RequireOpenDocumentAsync()
    {
        await WaitForDocumentAsync();

        return CurrentDocumentViewModel?.Content is SegmentTabViewModel or DictionaryTabViewModel
            ? CurrentDocumentViewModel.Content
            : throw new InvalidOperationException("The current document is not a segment or dictionary. Use columnstore_open first.");
    }

    private async Task WaitForDocumentAsync()
    {
        var waited = 0;

        while (waited < DocumentLoadTimeoutMs && !IsDocumentLoaded())
        {
            await Task.Delay(DocumentLoadPollMs);

            waited += DocumentLoadPollMs;
        }

        if (!IsDocumentLoaded())
        {
            throw new InvalidOperationException("The document is still loading.");
        }
    }

    private bool IsDocumentLoaded()
        => CurrentDocumentViewModel?.Content switch
        {
            SegmentTabViewModel segment => segment.IsLoaded && !segment.IsBusy,
            DictionaryTabViewModel dictionary => dictionary.IsLoaded,
            _ => true
        };
}