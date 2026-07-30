using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using AllocationUnit = InternalsViewer.Internals.Engine.Database.AllocationUnit;

namespace InternalsViewer.UI.App.ViewModels.Page;

/// <summary>
/// Projects a parsed page into the state the page tab binds to
/// </summary>
internal sealed class PageDisplayBuilder(ILogger logger, IRecordService recordService)
{
    public const int RowDataTabIndex = 1;
    public const int AllocationsTabIndex = 2;

    public const short PageHeaderSlot = -100;
    public const short IamHeaderSlot = -10;
    public const short CompressionInfoSlot = -90;

    private ILogger Logger { get; } = logger;

    private IRecordService RecordService { get; } = recordService;

    public PageDisplay Build(Internals.Engine.Pages.Page resultPage, short? slot, PageAddress pageAddress)
    {
        var headerSlot = new PageSlot
        {
            Index = PageHeaderSlot,
            Description = "Page Header"
        };

        var slots = resultPage.OffsetTable.Select((s, i) => new PageSlot
        {
            Index = (short)i,
            Offset = s,
            Description = $"0x{s:X}"
        }).ToList();

        slots.Insert(0, headerSlot);

        Logger.LogDebug("Building Offset Table");

        var display = new PageDisplay(resultPage, slots, slot);

        switch (resultPage)
        {
            case FileHeaderPage:
                break;
            case AllocationUnitPage allocationUnitPage:
                var records = LoadRecords(allocationUnitPage);

                if (allocationUnitPage.CompressionInfo != null)
                {
                    slots.Insert(1, new PageSlot
                    {
                        Index = CompressionInfoSlot,
                        Description = "Compression Info"
                    });
                }

                display = display with
                {
                    AllocationUnit = allocationUnitPage.AllocationUnit,
                    Records = records,
                    RecordsResultSet = RecordResultSetHelper.ToResultSet(records),
                    IsAllocationsTabVisible = false,
                    IsRowDataTabVisible = true,
                    IsPfsTabVisible = false,
                    TabSwitch = (AllocationsTabIndex, RowDataTabIndex)
                };
                break;
            case IamPage iamPage:
                slots.Insert(1, new PageSlot
                {
                    Index = IamHeaderSlot,
                    Description = "IAM Header"
                });

                display = display with
                {
                    AllocationUnit = iamPage.AllocationUnit,
                    AllocationLayer = BuildIamLayer(iamPage),
                    // IAMs are not necessarily in the same file as where they are tracking. The Start Page file determines the file
                    AllocationFileId = iamPage.StartPage.FileId,
                    IsRowDataTabVisible = true,
                    IsAllocationsTabVisible = true,
                    IsPfsTabVisible = false,
                    TabSwitch = (RowDataTabIndex, AllocationsTabIndex)
                };
                break;
            case AllocationPage allocationPage:
                display = display with
                {
                    AllocationLayer = BuildAllocationLayer(allocationPage),
                    AllocationFileId = allocationPage.PageAddress.FileId,
                    IsAllocationsTabVisible = true,
                    IsRowDataTabVisible = false,
                    IsPfsTabVisible = false,
                    TabSwitch = (RowDataTabIndex, AllocationsTabIndex)
                };
                break;
            case PfsPage pfsPage:
                display = display with
                {
                    PfsChain = new PfsChain { PfsPages = { pfsPage } },
                    AllocationFileId = pfsPage.PageAddress.FileId,
                    AllocationStartPage = pageAddress.PageId == 1 ? 0 : pageAddress.PageId,
                    IsAllocationsTabVisible = false,
                    IsRowDataTabVisible = true,
                    IsPfsTabVisible = true,
                    TabSwitch = (RowDataTabIndex, AllocationsTabIndex)
                };
                break;
            default:
                display = display with { AllocationUnit = null };
                break;
        }

        return display;
    }

    private static AllocationLayer BuildIamLayer(IamPage iamPage)
    {
        var layer = AllocationLayerBuilder.GenerateLayer(iamPage, iamPage.StartPage.PageId);

        layer.Name = $"IAM Page {iamPage.PageAddress}";
        layer.Colour = System.Drawing.Color.Brown;

        layer.IsVisible = true;

        return layer;
    }

    private static AllocationLayer BuildAllocationLayer(AllocationPage allocationPage)
    {
        var layer = AllocationLayerBuilder.GenerateLayer(allocationPage, 0);

        layer.Name = $"Allocation Page {allocationPage.PageAddress}";
        layer.Colour = System.Drawing.Color.Brown;

        layer.IsVisible = true;

        return layer;
    }

    private List<IRecord> LoadRecords(AllocationUnitPage target)
    {
        Logger.LogDebug("Loading Records");

        List<IRecord> records = [];

        try
        {
            records.AddRange(RecordService.GetRecords(target));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error loading record(s)");
        }

        Logger.LogDebug("{RecordCount} Record(s) loaded", records.Count);

        return records;
    }
}

internal sealed record PageDisplay(Internals.Engine.Pages.Page Page, List<PageSlot> Slots, short? Slot)
{
    public AllocationUnit? AllocationUnit { get; init; }

    public List<IRecord>? Records { get; init; }

    public QueryResultSet? RecordsResultSet { get; init; }

    public AllocationLayer? AllocationLayer { get; init; }

    public short? AllocationFileId { get; init; }

    public int? AllocationStartPage { get; init; }

    public PfsChain? PfsChain { get; init; }

    public bool? IsRowDataTabVisible { get; init; }

    public bool? IsAllocationsTabVisible { get; init; }

    public bool? IsPfsTabVisible { get; init; }

    public (int From, int To)? TabSwitch { get; init; }
}
