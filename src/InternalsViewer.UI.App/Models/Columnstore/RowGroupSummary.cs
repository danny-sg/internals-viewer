using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One row group and its segments, shared by the structure drawing and the row groups table
/// </summary>
public sealed class RowGroupSummary
{
    public required RowGroup RowGroup { get; init; }

    public int RowGroupId => RowGroup.RowGroupId;

    public RowGroupState State => RowGroup.State;

    public int TotalRows => RowGroup.TotalRows;

    public long SizeInBytes => RowGroup.SizeInBytes;

    public long DeltaStoreHobtId => RowGroup.DeltaStoreHobtId;

    public bool IsCompressed => RowGroup.IsCompressed;

    public IReadOnlyList<SegmentSummary> Segments { get; init; } = [];

    public static List<RowGroupSummary> Build(ColumnStoreIndex index)
    {
        var largest = index.RowGroups
                           .SelectMany(r => r.Segments)
                           .Select(s => s.OnDiskSize)
                           .DefaultIfEmpty(0)
                           .Max();

        return
        [
            .. index.RowGroups.Select(rowGroup => new RowGroupSummary
            {
                RowGroup = rowGroup,
                Segments = [.. rowGroup.Segments.Select(segment => new SegmentSummary
                {
                    Segment = segment,
                    LargestSegmentSize = largest
                })]
            })
        ];
    }
}
