using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.UI.App.vNext.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using InternalsViewer.UI.App.vNext.Helpers;
using CommunityToolkit.Mvvm.Input;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class PageViewModel(IServiceProvider serviceProvider,
                                   DatabaseSource database)
    : TabViewModel(serviceProvider, TabType.Page)
{
    public override TabType TabType => TabType.Page;

    public override ImageSource ImageSource => new BitmapImage(new Uri("ms-appx:///Assets/TabIcons/Page32.png"));

    [ObservableProperty]
    private string objectName = string.Empty;

    [ObservableProperty]
    private string indexName = string.Empty;

    [ObservableProperty]
    private string objectIndexType = string.Empty;

    [ObservableProperty]
    private string indexType = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private PageAddress pageAddress;

    [ObservableProperty]
    private PageAddress nextPage;

    [ObservableProperty]
    private PageAddress previousPage;

    [ObservableProperty]
    private Internals.Engine.Pages.Page page = new EmptyPage();

    [ObservableProperty]
    private DatabaseSource database = database;

    [ObservableProperty]
    private ObservableCollection<OffsetSlot> offsets = new();

    [ObservableProperty]
    private ushort? selectedOffset;

    [ObservableProperty]
    private Marker? selectedMarker;

    [ObservableProperty]
    private bool includeHeaderMarkers = false;

    [ObservableProperty]
    private ObservableCollection<Marker> markers = new();

    public List<Record> Records { get; set; } = new();

    partial void OnIncludeHeaderMarkersChanged(bool value)
    {
        AddPageMarkers(Page);
    }

    partial void OnSelectedOffsetChanged(ushort? value)
    {
        if (value == null)
        {
            Markers.Clear();
            return;
        }

        AddRecordMarkers(value.Value);
    }

    private void AddRecordMarkers(ushort value)
    {
        var record = Records.FirstOrDefault(r => r.SlotOffset == value);

        if (record is null)
        {
            return;
        }

        var pageMarkers = GetPageMarkers(Page);

        var recordMarkers = MarkerBuilder.BuildMarkers(record);

        Markers = new ObservableCollection<Marker>(pageMarkers.Concat(recordMarkers).OrderBy(o => o.StartPosition));
    }

    [RelayCommand]
    public async Task LoadPage(PageAddress address)
    {
        var logger = GetService<ILogger<PageViewModel>>();

        logger.LogDebug("Loading Page {Address}", address);

        IsLoading = true;

        var pageService = GetService<IPageService>();

        Name = $"Loading Page {address}...";
        PageAddress = address;

        var resultPage = await pageService.GetPage(Database, address);

        Name = $"{resultPage.PageHeader.PageTypeName} Page {address}";

        logger.LogDebug("Building Offset Table");

        var slots = resultPage.OffsetTable.Select((s, i) => new OffsetSlot
        {
            Index = (ushort)i,
            Offset = s,
            Description = $"0x{s:X}"
        });

        SelectedOffset = null;
        SelectedMarker = null;

        Offsets = new ObservableCollection<OffsetSlot>(slots);

        logger.LogDebug("Adding Page Markers");

        AddPageMarkers(resultPage);

        switch (resultPage)
        {
            case AllocationUnitPage dataPage:
                SetAllocationUnitDescription(dataPage);

                LoadRecords(dataPage);
                break;
            default:
                IndexName = string.Empty;
                ObjectName = string.Empty;
                IndexType = string.Empty;
                ObjectIndexType = string.Empty;
                break;
        }

        Page = resultPage;

        NextPage = new PageAddress(pageAddress.FileId, pageAddress.PageId + 1);

        if (pageAddress.PageId > 0)
        {
            PreviousPage = new PageAddress(pageAddress.FileId, pageAddress.PageId - 1);
        }

        IsLoading = false;
    }

    private void SetAllocationUnitDescription(AllocationUnitPage dataPage)
    {
        ObjectName = $"{dataPage.AllocationUnit.SchemaName}.{dataPage.AllocationUnit.TableName}";

        IndexName = dataPage.AllocationUnit.IndexName;
        IndexType = dataPage.AllocationUnit.IndexType == Internals.Engine.Database.Enums.IndexType.NonClustered
                                                         ? "Non-Clustered"
                                                         : string.Empty;
        ObjectIndexType = dataPage.AllocationUnit.ParentIndexType == Internals.Engine.Database.Enums.IndexType.Clustered
                                                         ? "Clustered"
                                                         : "Heap";
    }

    private void LoadRecords(Internals.Engine.Pages.Page target)
    {
        var logger = GetService<ILogger<PageViewModel>>();
        logger.LogDebug("Loading Records");

        Records.Clear();

        var recordService = GetService<IRecordService>();

        foreach (var slot in Offsets)
        {
            try
            {
                switch (target.PageHeader.PageType)
                {
                    case PageType.Data:
                        logger.LogDebug("Loading Data Records");

                        Records.Add(recordService.GetDataRecord((DataPage)target, slot.Offset));
                        break;
                    case PageType.Index:
                        logger.LogDebug("Loading Index Records");

                        Records.Add(recordService.GetIndexRecord((IndexPage)target, slot.Offset));
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error loading record {slot.Index}");
            }
        }

        logger.LogDebug("{RecordCount} Record(s) loaded", Records.Count);
    }

    /// <summary>
    /// Add the header and offset table markers (applies to all pages)
    /// </summary>
    private void AddPageMarkers(Internals.Engine.Pages.Page p)
    {
        var m = GetPageMarkers(p);

        Markers = new ObservableCollection<Marker>(m);
    }

    private List<Marker> GetPageMarkers(Internals.Engine.Pages.Page p)
    {
        var m = new List<Marker>();

        m.Add(new Marker
        {
            Name = "Page Header",
            StartPosition = 0,
            EndPosition = 95,
            ForeColour = Color.Blue,
            BackColour = Color.FromArgb(245, 245, 250)
        });

        if (IncludeHeaderMarkers)
        {
            var headerMarkers = MarkerBuilder.BuildMarkers(p.PageHeader);

            m.AddRange(headerMarkers);
        }

        var offsetTableStart = PageData.Size - p.PageHeader.SlotCount * 2;

        m.Add(new Marker
        {
            Name = "Offset Table",
            StartPosition = offsetTableStart,
            EndPosition = PageData.Size,
            ForeColour = Color.Green,
            BackColour = Color.FromArgb(245, 250, 245)
        });

        return m;
    }
}
