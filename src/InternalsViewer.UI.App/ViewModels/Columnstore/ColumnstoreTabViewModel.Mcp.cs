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
            SelectedMarkerOffset = marker?.StartPosition,
            SelectedMarkerLength = marker is null ? null : marker.EndPosition - marker.StartPosition + 1,
            IsLoaded = IsInitialized,
            SelectedRow = content switch
            {
                SegmentTabViewModel segment => segment.SelectedRow?.Ordinal,
                DictionaryTabViewModel { SelectedEntryIndex: >= 0 } dictionary => dictionary.SelectedEntryIndex,
                _ => null
            }
        };
    }

    public async Task OpenAsync(ColumnstoreItem item, int? rowGroup, int? column, int? dictionary)
    {
        RequireInitialized();

        switch (item)
        {
            case ColumnstoreItem.Segment:
            {
                var segment = Segments.FirstOrDefault(s => s.RowGroupId == rowGroup && s.ColumnId == column)
                              ?? throw new InvalidOperationException($"No segment for row group {rowGroup} column {column}.");

                OpenSegment(segment);

                await WaitForDocumentAsync();

                break;
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

                break;
            }

            case ColumnstoreItem.DeltaStore:
            {
                var group = RowGroups.FirstOrDefault(r => r.RowGroupId == rowGroup)
                            ?? throw new InvalidOperationException($"No row group {rowGroup}.");

                OpenDeltaStore(group);

                break;
            }

            default:
                OpenDeleteBitmap();

                break;
        }
    }

    public Task ShowDocumentAsync(string name)
    {
        var documents = Dock.Groups().SelectMany(g => g.Documents).ToList();

        var document = documents.FirstOrDefault(d => string.Equals(d.Title, name, StringComparison.OrdinalIgnoreCase))
                       ?? documents.FirstOrDefault(d => d.Title.Contains(name, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException($"No open document matches '{name}'.");

        var group = Dock.Groups().First(g => g.Documents.Contains(document));

        group.SelectedDocument = document;

        Dock.NotifySelectionChanged();

        return Task.CompletedTask;
    }

    public Task ShowMetadataTabAsync(ColumnstoreMetadataTab tab)
    {
        SelectedMetadataTabIndex = (int)tab;

        return Task.CompletedTask;
    }

    public async Task ShowRegionAsync(string region)
    {
        var content = await RequireOpenDocumentAsync();

        switch (content)
        {
            case SegmentTabViewModel segment:
            {
                var index = Array.FindIndex(SegmentRegionNames, n => string.Equals(n, region, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                {
                    throw new InvalidOperationException($"No region '{region}'.");
                }

                if (index < SegmentDataTabIndex)
                {
                    segment.Region = (SegmentRegion)index;
                }

                segment.SelectedRegionTabIndex = index;

                break;
            }

            case DictionaryTabViewModel dictionary:
            {
                var index = Array.FindIndex(DictionaryRegionNames, n => string.Equals(n, region, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                {
                    throw new InvalidOperationException($"No region '{region}'.");
                }

                dictionary.SelectedTabIndex = index;

                break;
            }

            default:
                throw new InvalidOperationException("The current document has no regions.");
        }
    }

    public async Task SelectMarkerAsync(string? name)
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

            return;
        }

        var markers = FlattenMarkers(hex.Markers).ToList();

        hex.SelectedMarker = markers.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                             ?? markers.FirstOrDefault(m => m.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                             ?? throw new InvalidOperationException($"No marker matches '{name}'.");
    }

    public async Task SelectRowAsync(int? index)
    {
        switch (await RequireOpenDocumentAsync())
        {
            case SegmentTabViewModel segment:
            {
                if (index is null)
                {
                    segment.SelectRow(null);

                    return;
                }

                if (segment.Rows is not { } rows || index < 0 || index >= rows.Count)
                {
                    throw new InvalidOperationException($"Row {index} is outside the segment, which has {segment.Rows?.Count ?? 0} rows.");
                }

                segment.SelectedRegionTabIndex = SegmentDataTabIndex;

                segment.SelectRow(rows[index.Value]);

                return;
            }

            case DictionaryTabViewModel dictionary:
            {
                if (index is null)
                {
                    dictionary.SelectedEntryIndex = -1;

                    return;
                }

                if (dictionary.Blob is not { } blob || index < 0 || index >= blob.EntryCount)
                {
                    throw new InvalidOperationException(
                        $"Entry {index} is outside the dictionary, which has {dictionary.Blob?.EntryCount ?? 0} entries.");
                }

                dictionary.SelectedTabIndex = DictionaryRegionNames.Length - 1;

                dictionary.SelectedEntryIndex = index.Value;

                return;
            }

            default:
                throw new InvalidOperationException("The current document has no rows.");
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
            : throw new InvalidOperationException("The current document is not a segment or dictionary.");
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