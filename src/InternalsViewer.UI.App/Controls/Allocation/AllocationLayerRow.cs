using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Controls.Allocation;

public enum AllocationLayerRowKind
{
    Layer,
    Index,
    Unit
}

public sealed partial class AllocationLayerRow : ObservableObject
{
    private AllocationLayerRow(AllocationLayer layer,
                               AllocationLayerRowKind kind,
                               string key,
                               int depth,
                               IReadOnlyList<AllocationLayerUnit> units,
                               string indexName,
                               string indexTypeDescription,
                               IReadOnlyList<AllocationLayerRow> children,
                               bool isExpanded)
    {
        Layer = layer;
        Kind = kind;
        Key = key;
        Depth = depth;
        Units = units;
        IndexName = indexName;
        IndexTypeDescription = indexTypeDescription;
        Children = children;
        _isExpanded = isExpanded;
    }

    public static AllocationLayerRow ForLayer(AllocationLayer layer, IReadOnlyDictionary<string, bool> expansionOverrides)
    {
        var key = $"L:{layer.Name}";

        var indexGroups = layer.Units
                               .GroupBy(u => (u.IndexName, u.IndexType))
                               .ToList();

        var children = indexGroups.Count > 1
                       ? [.. indexGroups.Select(g => ForIndex(layer, [.. g], expansionOverrides))]
                       : UnitRows(layer, layer.Units, depth: 1);

        return new AllocationLayerRow(layer,
                                      AllocationLayerRowKind.Layer,
                                      key,
                                      depth: 0,
                                      layer.Units,
                                      layer.IndexName,
                                      layer.IndexTypeDescription,
                                      children,
                                      IsExpandedByDefault(key, layer.IndexType, expansionOverrides));
    }

    private static AllocationLayerRow ForIndex(AllocationLayer layer,
                                               List<AllocationLayerUnit> units,
                                               IReadOnlyDictionary<string, bool> expansionOverrides)
    {
        var first = units[0];

        var key = $"I:{layer.Name}|{first.IndexName}";

        return new AllocationLayerRow(layer,
                                      AllocationLayerRowKind.Index,
                                      key,
                                      depth: 1,
                                      units,
                                      first.IndexName,
                                      first.IndexTypeDescription,
                                      UnitRows(layer, units, depth: 2),
                                      IsExpandedByDefault(key, first.IndexType, expansionOverrides));
    }

    private static bool IsExpandedByDefault(string key, IndexType indexType, IReadOnlyDictionary<string, bool> expansionOverrides)
    {
        if (expansionOverrides.TryGetValue(key, out var isExpanded))
        {
            return isExpanded;
        }

        return indexType is not (IndexType.ClusteredColumnStore or IndexType.NonClusteredColumnStore);
    }

    private static IReadOnlyList<AllocationLayerRow> UnitRows(AllocationLayer layer, IReadOnlyList<AllocationLayerUnit> units, int depth)
    {
        if (units.Count <= 1)
        {
            return [];
        }

        return [.. units.Select(u => new AllocationLayerRow(layer,
                                                            AllocationLayerRowKind.Unit,
                                                            $"U:{u.AllocationUnitId}",
                                                            depth,
                                                            [u],
                                                            u.PartitionNumber is { } partition ? $"Partition {partition}" : string.Empty,
                                                            string.Empty,
                                                            [],
                                                            isExpanded: false))];
    }

    public AllocationLayer Layer { get; }

    public AllocationLayerRowKind Kind { get; }

    public string Key { get; }

    public int Depth { get; }

    public IReadOnlyList<AllocationLayerUnit> Units { get; }

    public IReadOnlyList<AllocationLayerRow> Children { get; }

    public bool HasChildren => Children.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpanderGlyph))]
    private bool _isExpanded;

    public Thickness Indent => new(Depth * 16, 0, 0, 0);

    public Visibility ExpanderVisibility => HasChildren ? Visibility.Visible : Visibility.Collapsed;

    public Visibility KeyVisibility => Kind == AllocationLayerRowKind.Layer ? Visibility.Visible : Visibility.Collapsed;

    public string ExpanderGlyph => ((char)(IsExpanded ? 0xE70D : 0xE76C)).ToString();

    public string ObjectName => Kind switch
    {
        AllocationLayerRowKind.Layer => Layer.ObjectName,
        AllocationLayerRowKind.Unit => Units[0].ColumnstoreUsage,
        _ => string.Empty
    };

    public string IndexName { get; }

    public string IndexTypeDescription { get; }

    public string TypeDescription => Kind == AllocationLayerRowKind.Unit ? Units[0].AllocationUnitTypeDescription : IndexTypeDescription;

    public Thickness TextMargin => new(Kind == AllocationLayerRowKind.Unit ? 28 : 12, 0, 12, 0);

    public long TotalPages => Kind == AllocationLayerRowKind.Layer ? Layer.TotalPages : Units.Sum(u => u.TotalPages);

    public long AllocationUnitId => DefaultUnit?.AllocationUnitId ?? Layer.AllocationUnitId;

    public PageAddress RootPage => DefaultUnit?.RootPage ?? PageAddress.Empty;

    public PageAddress FirstPage => DefaultUnit?.FirstPage ?? PageAddress.Empty;

    public PageAddress FirstIamPage => DefaultUnit?.FirstIamPage ?? PageAddress.Empty;

    public bool HasEntryPoints => DefaultUnit is not null;

    public bool IsIndex => DefaultUnit?.IsIndex ?? false;

    public bool IsColumnstore => DefaultUnit?.IsColumnstore ?? false;

    private AllocationLayerUnit? DefaultUnit
    {
        get
        {
            if (Kind == AllocationLayerRowKind.Unit)
            {
                return Units[0];
            }

            return Layer.IsPartitioned || Units.Count == 0 ? null : Units[0];
        }
    }
}
