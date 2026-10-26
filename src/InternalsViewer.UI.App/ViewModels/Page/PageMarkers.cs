using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Annotations;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Markers;
using Microsoft.UI;
using Windows.UI;

namespace InternalsViewer.UI.App.ViewModels.Page;

internal static class PageMarkers
{
    private const int HeaderEnd = 95;

    private const int UnusedStart = 64;

    public static List<Marker> GetHeaderMarkers(PageData page)
    {
        var markers = MarkerBuilder.BuildMarkers(page.PageHeader);

        markers.Add(new Marker
        {
            Name = "Unused",
            StartPosition = UnusedStart,
            EndPosition = HeaderEnd,
            ForeColour = Colors.Gray,
            BackColour = Colors.AliceBlue,
            IsVisible = false
        });

        return markers;
    }

    public static (List<Marker> Markers, ushort Start) GetOffsetTableMarkers(PageData page)
    {
        var markers = new List<Marker>();

        var slotMarkers = new List<Marker>(page.OffsetTable.Length);

        var start = PageData.Size - page.PageHeader.SlotCount * 2;

        var offset = PageData.Size - 2;

        for (uint slotIndex = 0; slotIndex < page.PageHeader.SlotCount; slotIndex++)
        {
            markers.Add(new Marker
            {
                Name = $"Slot {slotIndex}",
                Value = page.OffsetTable[slotIndex].ToString(),
                StartPosition = offset,
                EndPosition = offset + 1,
                ForeColour = Colors.Green,
                BackColour = slotIndex % 2 == 0 ? Color.FromArgb(50, 205, 250, 205) : Color.FromArgb(255, 215, 255, 215),
                IsVisible = true,
                HasKey = true,
            });

            offset -= 2;
        }

        markers.Add(new Marker
        {
            Name = "Offset Table",
            StartPosition = start,
            EndPosition = PageData.Size,
            ForeColour = Colors.Green,
            BackColour = Color.FromArgb(100, 205, 250, 205),
            IsVisible = false,
            Children = new ObservableCollection<Marker>(slotMarkers)
        });

        return (markers, (ushort)start);
    }

    public static List<Marker> GetRecordMarkers(PageData page, IDataStructure record)
        => [.. GetPageMarkers(page).Concat(MarkerBuilder.BuildMarkers(record)).OrderBy(o => o.StartPosition)];

    private static List<Marker> GetPageMarkers(PageData page)
        =>
        [
            new()
            {
                Name = "Page Header",
                StartPosition = 0,
                EndPosition = HeaderEnd,
                ForeColour = Colors.Blue,
                BackColour = Color.FromArgb(1, 245, 245, 250),
                IsVisible = false
            },
            new()
            {
                Name = "Offset Table",
                StartPosition = PageData.Size - page.PageHeader.SlotCount * 2,
                EndPosition = PageData.Size,
                ForeColour = Colors.Green,
                BackColour = Color.FromArgb(50, 205, 250, 205),
                IsVisible = false
            }
        ];
}
