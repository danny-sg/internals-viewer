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

public partial class PageViewModel(MainViewModel parent,
        DatabaseSource database,
        ILogger<PageViewModel> logger)
    : TabViewModel(parent, TabType.Page)
{
    public ILogger<PageViewModel> Logger { get; } = logger;

    public override TabType TabType => TabType.Page;

    public override ImageSource ImageSource => new BitmapImage(new Uri("ms-appx:///Assets/TabIcons/Page32.png"));

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private PageAddress pageAddress;

    [ObservableProperty]
    private PageAddress nextPage;

    [ObservableProperty]
    private PageAddress previousPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageData))]
    [NotifyPropertyChangedFor(nameof(Offsets))]
    private Internals.Engine.Pages.Page? page;

    [ObservableProperty]
    private DatabaseSource database = database;

    [ObservableProperty]
    private List<OffsetSlot> offsets = new();

    [ObservableProperty]
    private ushort selectedOffset;

    public byte[] PageData => Page?.Data ?? Array.Empty<byte>();

    [ObservableProperty]
    private ObservableCollection<Marker> markers = new();

    public List<Record> Records { get; set; } = new();

    partial void OnSelectedOffsetChanged(ushort value)
    {
        AddRecordMarkers(value);
    }

    private void AddRecordMarkers(ushort value)
    {
        var record = Records.FirstOrDefault(r => r.SlotOffset == value);

        if (record is null)
        {
            return;
        }

        var pageMarkers = GetPageMarkers(Offsets.Count);

        var recordMarkers = MarkerBuilder.BuildMarkers(record);

        Markers = new ObservableCollection<Marker>(pageMarkers.Concat(recordMarkers).OrderBy(o => o.StartPosition));
    }

    [RelayCommand]
    public async Task LoadPage(PageAddress address)
    {
        Logger.LogDebug("Loading Page {Address}", address);

        IsLoading = true;

        var pageService = Parent.GetService<IPageService>();

        Name = $"Page {address}";
        PageAddress = address;

        var resultPage = await pageService.GetPage(Database, address);

        Logger.LogDebug("Building Offset Table");

        Offsets = resultPage.OffsetTable.Select((s, i) => new OffsetSlot
        {
            Index = (ushort)i,
            Offset = s,
            Description = $"0x{s:X}"
        }).ToList();

        Logger.LogDebug("Adding Page Markers");

        AddPageMarkers(resultPage.PageHeader.SlotCount);

        switch (resultPage.PageHeader)
        {
            case { PageType: PageType.Data }:
            case { PageType: PageType.Index }:
                LoadRecords(resultPage);
                break;
        }

        Page = resultPage;

        NextPage = new PageAddress(pageAddress.FileId, pageAddress.PageId + 1);

        if(pageAddress.PageId > 0)
        {
            PreviousPage = new PageAddress(pageAddress.FileId, pageAddress.PageId - 1);
        }

        IsLoading = false;
    }

    private void LoadRecords(Internals.Engine.Pages.Page target)
    {
        Logger.LogDebug("Loading Records");

        Records.Clear();

        var recordService = Parent.GetService<IRecordService>();

        foreach (var slot in Offsets)
        {
            try
            {
                switch (target.PageHeader.PageType)
                {
                    case PageType.Data:
                        Logger.LogDebug("Loading Data Records");

                        Records.Add(recordService!.GetDataRecord((DataPage)target, slot.Offset));
                        break;
                    case PageType.Index:
                        Logger.LogDebug("Loading Index Records");

                        Records.Add(recordService!.GetIndexRecord((IndexPage)target, slot.Offset));
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error loading record {slot.Index}");
            }
        }

        Logger.LogDebug("{RecordCount} Record(s) loaded", Records.Count);
    }

    /// <summary>
    /// Add the header and offset table markers (applies to all pages)
    /// </summary>
    private void AddPageMarkers(int slotCount)
    {
        var m = GetPageMarkers(slotCount);

        Markers = new ObservableCollection<Marker>(m);
    }

    private List<Marker> GetPageMarkers(int slotCount)
    {
        var m = new List<Marker>();

        m.Add(new Marker
        {
            Name = "Page Header",
            StartPosition = 0,
            EndPosition = 96,
            ForeColour = Color.Blue,
            BackColour = Color.FromArgb(245, 245, 250)
        });

        var offsetTableStart = PageData.Length - slotCount * 2;

        m.Add(new Marker
        {
            Name = "Offset Table",
            StartPosition = offsetTableStart,
            EndPosition = PageData.Length,
            ForeColour = Color.Green,
            BackColour = Color.FromArgb(245, 250, 245)
        });
        return m;
    }
}
