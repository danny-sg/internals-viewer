using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.UI.App.vNext.Models;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class PageViewModel(MainViewModel parent,
                                   DatabaseSource database) : TabViewModel(parent, TabType.Page)
{
    public override TabType TabType => TabType.Page;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private PageAddress pageAddress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageData))]
    private Internals.Engine.Pages.Page? page;

    [ObservableProperty]
    private DatabaseSource database = database;

    [ObservableProperty]
    private List<OffsetSlot> offsets = new();

    public byte[] PageData => Page?.Data ?? Array.Empty<byte>();

    [ObservableProperty]
    private ObservableCollection<Marker> markers = new();

    public async Task LoadPage(PageAddress address)
    {
        IsLoading = true;

        var pageService = Parent.ServiceProvider.GetService<IPageService>();

        if (pageService == null)
        {
            throw new InvalidOperationException("Page Service not registered");
        }

        Name = $"Page {address}";
        PageAddress = address;

        Page = await pageService.GetPage(Database, address);

        Offsets = Page.OffsetTable.Select((s, i) => new OffsetSlot
        {
            Index = (ushort)i,
            Offset = s,
            Description = $"0x{s:X}"
        }).ToList();

        LoadMarkers();

        IsLoading = false;
    }

    private void LoadMarkers()
    {
        var m = new List<Marker>();

        m.Add(new Marker
        {
            StartPosition = 0,
            EndPosition = 96,
            ForeColour = Color.Blue,
            BackColour = Color.FromArgb(245, 245, 250)
        });

        var offsetTableStart = PageData.Length - Page!.PageHeader.SlotCount * 2;

        m.Add(new Marker { StartPosition = offsetTableStart, EndPosition = PageData.Length, ForeColour = Color.Green, BackColour = Color.FromArgb(245, 250, 245) });
    
        Markers = new ObservableCollection<Marker>(m);
    }
}
