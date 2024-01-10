using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.TransactionLog;
using InternalsViewer.UI.Controls;
using InternalsViewer.UI.Markers;
using InternalsViewer.UI.Renderers;

namespace InternalsViewer.UI;

public partial class PageViewerWindow : UserControl
{
    public IRecordService RecordService { get; set; }

    public event EventHandler<PageEventArgs>? PageChanged;

    public event EventHandler? OpenDecodeWindow;

    private readonly ProfessionalColorTable colourTable;

    private readonly PfsRenderer pfsRenderer;

    public int SearchPosition { get; private set; }

    public Dictionary<string, LogData> Data { get; private set; } = new();

    public PfsByte? PfsByte { get; set; }

    public Page? Page { get; set; }

    public IPageService PageService { get; set; }

    public DatabaseSource Database { get;  }

    /// <summary>
    /// Initializes a new instance of the <see cref="PageViewerWindow"/> class.
    /// </summary>
    public PageViewerWindow(IPageService pageService, IRecordService recordService, DatabaseSource database)
    {
        Database = database;

        RecordService = recordService;
        PageService = pageService;

        InitializeComponent();

        colourTable = new ProfessionalColorTable();
        colourTable.UseSystemColors = true;

        pfsRenderer = new PfsRenderer(new Rectangle(1, 1, 34, 34), Color.White, SystemColors.ControlDark);
    }

    /// <summary>
    /// Refreshes the current page in the page viewer
    /// </summary>
    private void RefreshPage()
    {
        if (Page == null)
        {
            return;
        }

        pageToolStripTextBox.Text = Page.PageAddress.ToString();
        hexViewer.Page = Page;
        pageBindingSource.DataSource = Page.PageHeader;
        offsetTable.Page = Page;

        if (Page is AllocationUnitPage allocationUnitPage)
        {
            ObjectIdTextBox.Text = allocationUnitPage.AllocationUnit.ObjectId.ToString();

            ObjectNameTextBox.Text = allocationUnitPage.AllocationUnit.DisplayName;

            PartitionIdTextBox.Text = allocationUnitPage.AllocationUnit.PartitionId.ToString();

            if (allocationUnitPage.CompressionType == CompressionType.Page && !Page.OffsetTable.Contains(96))
            {
                compressionInfoPanel.Visible = true;
            }
            else
            {
                compressionInfoPanel.Visible = false;
            }
        }


        OnPageChanged(this, new PageEventArgs(Page.PageAddress, false));
    }

    /// <summary>
    /// Refreshes the allocation status from the various allocation pages
    /// </summary>
    /// <param name="pageAddress">The page address.</param>
    private void RefreshAllocationStatus(PageAddress pageAddress)
    {
        if (Page == null)
        {
            return;
        }

        Image unallocated = ExtentColour.KeyImage(Color.Gainsboro);
        Image gamAllocated = ExtentColour.KeyImage(Color.FromArgb(172, 186, 214));
        Image sGamAllocated = ExtentColour.KeyImage(Color.FromArgb(168, 204, 162));
        Image dcmAllocated = ExtentColour.KeyImage(Color.FromArgb(120, 150, 150));
        Image bcmAllocated = ExtentColour.KeyImage(Color.FromArgb(150, 120, 150));

        var fileId = pageAddress.FileId;

        gamPictureBox.Image = Page.Database.Gam[fileId].IsExtentAllocated(pageAddress.Extent, fileId, false) ? unallocated : gamAllocated;
        sGamPictureBox.Image = Page.Database.SGam[fileId].IsExtentAllocated(pageAddress.Extent, fileId, false) ? unallocated : sGamAllocated;
        dcmPictureBox.Image = Page.Database.Dcm[fileId].IsExtentAllocated(pageAddress.Extent, fileId, false) ? unallocated : dcmAllocated;
        bcmPictureBox.Image = Page.Database.Bcm[fileId].IsExtentAllocated(pageAddress.Extent, fileId, false) ? unallocated : bcmAllocated;

        //gamTextBox.Text = page.AllocationStatus.GamPageAddress.ToString();
        //sgamTextBox.Text = page.AllocationStatus.SgamPageAddress.ToString();
        //dcmTextBox.Text = page.AllocationStatus.DcmPageAddress.ToString();
        //bcmTextBox.Text = page.AllocationStatus.BcmPageAddress.ToString();
        //pfsTextBox.Text = page.AllocationStatus.PfsPageAddress.ToString();

        //pfsByte = Page.PfsStatus();

        pfsPanel.Invalidate();
    }

    public async Task LoadPage(RowIdentifier rowIdentifier)
    {
        await LoadPage(rowIdentifier.PageAddress);

        offsetTable.SelectedSlot = rowIdentifier.SlotId;
    }

    public async Task LoadPage(PageAddress pageAddress)
    {
        if (pageAddress.FileId == 0)
        {
            return;
        }

        pageAddressToolStripStatusLabel.Text = string.Empty;
        offsetToolStripStatusLabel.Text = string.Empty;

        Page = await PageService.GetPage(Database, pageAddress);

        RefreshAllocationStatus(Page.PageAddress);

        pageToolStripTextBox.Text = Page.PageAddress.ToString();

        serverToolStripStatusLabel.Text = Page.Database.Name;

        switch (Page.PageHeader.PageType)
        {
            case PageType.Data:
            case PageType.Index:
                markerKeyTable.Visible = true;
                allocationViewer.Visible = false;
                break;
            case PageType.Iam:
            case PageType.Gam:
            case PageType.Sgam:
            case PageType.Bcm:
            case PageType.Dcm:

                allocationViewer.SetAllocationPage((Page as AllocationPage)!, Page.PageHeader.PageType == PageType.Iam);

                markerKeyTable.Visible = false;
                allocationViewer.Visible = true;
                break;

            case PageType.Pfs:
                allocationViewer.SetPfsPage((Page as PfsPage)!);

                markerKeyTable.Visible = false;
                allocationViewer.Visible = true;
                break;
            case PageType.None:
                markerKeyTable.Visible = false;
                allocationViewer.Visible = false;
                break;
        }

        RefreshPage();
    }

    private async void PageTextBox_MouseClick(object sender, MouseEventArgs e)
    {
        await LoadPage(PageAddressParser.Parse(((TextBox)sender).Text));
    }

    private void OffsetTable_SlotChanged(object sender, EventArgs e)
    {
        if (offsetTable.SelectedOffset > 0)
        {
            compressionInfoTable.SelectedStructure = CompressionInfoStructure.None;

            LoadRecord(offsetTable.SelectedOffset);
        }
    }

    private void LoadRecord(ushort offset)
    {
        Record? record = null;

        switch (Page?.PageHeader.PageType)
        {
            case PageType.Data:

                record = RecordService.GetDataRecord((DataPage)Page, offset);

                allocationViewer.Visible = false;
                markerKeyTable.Visible = true;
                break;

            case PageType.Index:

                record = RecordService.GetIndexRecord((IndexPage)Page, offset);

                allocationViewer.Visible = false;
                markerKeyTable.Visible = true;
                break;

            case PageType.Iam:
            case PageType.Gam:
            case PageType.Sgam:
            case PageType.Bcm:
            case PageType.Dcm:
            case PageType.Pfs:

                markerKeyTable.Visible = false;
                allocationViewer.Visible = true;
                break;

            case PageType.Lob3:
            case PageType.Lob4:

                //record = new BlobRecord(Page, offset);

                allocationViewer.Visible = false;
                markerKeyTable.Visible = true;
                break;
        }

        if (record != null)
        {
            var markers = MarkerBuilder.BuildMarkers(record);

            hexViewer.AddMarkers(markers);

            markerKeyTable.SetMarkers(markers);

            hexViewer.ScrollToOffset(offset);

            offsetTableToolStripTextBox.Text = $@"{offset:0000}";
        }
    }

    public void SetSlot(int slotId)
    {
        offsetTable.SelectedSlot = slotId;
    }

    private void FindRecord(int offset)
    {
        if (Page == null)
        {
            return;
        }

        var sortedOffsetTable = new List<ushort>(Page.OffsetTable.ToArray());

        sortedOffsetTable.Sort();

        ushort currentOffset = 0;

        foreach (short i in sortedOffsetTable)
        {
            if (offset < i)
            {
                break;
            }

            currentOffset = (ushort)i;
        }

        if (currentOffset > 0)
        {
            offsetTable.SelectedSlot = Page.OffsetTable.IndexOf(currentOffset);
        }
    }

    public void FindNext(string findHex, bool suppressContinue)
    {
        var hexString = hexViewer.Text.Replace(" ", string.Empty).Replace("\n", string.Empty);

        var startPosition = hexString.IndexOf(findHex, SearchPosition + 1, StringComparison.Ordinal);

        if (startPosition > 0)
        {
            if (startPosition % 2 == 0)
            {
                var endPosition = startPosition + findHex.Length - 1;
                SearchPosition = endPosition;

                hexViewer.SetSelection(startPosition / 2, endPosition / 2);
            }
            else if (!suppressContinue)
            {
                FindNext(findHex, true);
            }
        }
        else
        {
            SearchPosition = 0;
            MessageBox.Show("Search hex not found", "Find", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }

    private async void PageToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Return)
        {
            try
            {
                await LoadPage(PageAddressParser.Parse(pageToolStripTextBox.Text));
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
            }
        }
    }

    private async void PreviousToolStripButton_Click(object sender, EventArgs e)
    {
        if (Page is { PageAddress.PageId: > 1 })
        {
            await LoadPage(new PageAddress(Page.PageAddress.FileId, Page.PageAddress.PageId - 1));
        }
    }

    private async void NextToolStripButton_Click(object sender, EventArgs e)
    {
        if (Page != null)
        {
            await LoadPage(new PageAddress(Page.PageAddress.FileId, Page.PageAddress.PageId + 1));
        }
    }

    private async void MarkerKeyTable_PageNavigated(object sender, PageEventArgs e)
    {
        await LoadPage(e.RowId);
    }

    private void MarkerKeyTable_SelectionChanged(object sender, EventArgs e)
    {
        hexViewer.SelectMarker(markerKeyTable.SelectedMarker);
    }

    private void MarkerKeyTable_SelectionClicked(object sender, EventArgs e)
    {
        hexViewer.HideToolTip();
    }

    private void HexViewer_OffsetOver(object sender, OffsetEventArgs e)
    {
        if (Page == null)
        {
            return;
        }

        switch (Page.PageHeader.PageType)
        {
            case PageType.Bcm:
            case PageType.Dcm:
            case PageType.Gam:
            case PageType.Iam:
            case PageType.Sgam:

                if (e.Offset >= AllocationPage.AllocationArrayOffset)
                {
                    var startExtent = (e.Offset - AllocationPage.AllocationArrayOffset) * 8;
                    var endExtent = startExtent + 7;

                    markerDescriptionToolStripStatusLabel.Text =
                        $"Extents {startExtent} - {endExtent} | Pages {startExtent * 8} - {(endExtent * 8) + 7}";
                }
                else
                {
                    markerDescriptionToolStripStatusLabel.Text = e.MarkerDescription;
                }

                break;

            default:

                markerDescriptionToolStripStatusLabel.Text = e.MarkerDescription;
                break;
        }

        markerDescriptionToolStripStatusLabel.ForeColor = e.ForeColour;
        markerDescriptionToolStripStatusLabel.BackColor = e.BackColour;

        offsetToolStripStatusLabel.Text = $"Offset: {e.Offset:0000}";
    }

    private void DisplayCompressionInfoStructure(CompressionInfoStructure compressionInfoStructure)
    {
        if (Page is not AllocationUnitPage allocationUnitPage || allocationUnitPage.CompressionInfo == null)
        {
            return;
        }

        var markers = new List<Marker>();

        switch (compressionInfoStructure)
        {
            case CompressionInfoStructure.Anchor:

                if (allocationUnitPage.CompressionInfo?.AnchorRecord != null)
                {
                    // markers = MarkerBuilder.BuildMarkers(Page.CompressionInfo.AnchorRecord);
                }

                break;

            case CompressionInfoStructure.Dictionary:

                if (allocationUnitPage.CompressionInfo is { HasDictionary: true })
                {
                    // markers = MarkerBuilder.BuildMarkers(Page.CompressionInfo.CompressionDictionary);
                }

                break;

            case CompressionInfoStructure.Header:

                markers = MarkerBuilder.BuildMarkers(allocationUnitPage.CompressionInfo);
                break;
        }

        hexViewer.AddMarkers(markers);

        markerKeyTable.SetMarkers(markers);
    }

    private void AllocationViewer_PageOver(object sender, PageEventArgs e)
    {
        if (Page?.PageHeader.PageType == PageType.Pfs)
        {
            pageAddressToolStripStatusLabel.Text = e.Address.ToString(); //+ " " + this.allocationViewer.allocationContainer.PagePfsByte(e.Address).ToString();
        }
        else
        {
            pageAddressToolStripStatusLabel.Text = e.Address.ToString();
        }
    }

    private async void AllocationViewer_PageClicked(object sender, PageEventArgs e)
    {
        await LoadPage(e.Address);
    }

    private void OffsetTableToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Return)
        {
            if (ushort.TryParse(offsetTableToolStripTextBox.Text, out var offset) && offset is < PageData.Size and > 0)
            {
                LoadRecord(offset);
            }
        }
    }

    private void PfsPanel_Paint(object sender, PaintEventArgs e)
    {
        if (PfsByte != null)
        {
            pfsRenderer.DrawPfsPage(e.Graphics, new Rectangle(0, 0, 32, 32), PfsByte);
        }
    }

    private void HexViewer_RecordFind(object sender, OffsetEventArgs e)
    {
        int offset = e.Offset;

        FindRecord(offset);
    }

    private void HexViewer_OffsetSet(object sender, OffsetEventArgs e)
    {
        LoadRecord(e.Offset);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var brush = new LinearGradientBrush(ClientRectangle,
                                                  colourTable.ToolStripGradientBegin,
                                                  colourTable.ToolStripGradientEnd,
                                                  LinearGradientMode.Horizontal);

        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    internal virtual void OnPageChanged(object sender, PageEventArgs e)
    {
        PageChanged?.Invoke(sender, e);
    }

    internal virtual void OnOpenDecodeWindow(object sender, PageEventArgs e)
    {
        OpenDecodeWindow?.Invoke(sender, e);
    }

    private void CompressionInfoTable_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        offsetTable.SelectedSlot = -1;

        DisplayCompressionInfoStructure(compressionInfoTable.SelectedStructure);
    }

    private void EncodeAndFindToolStripButton_Click(object sender, EventArgs e)
    {
        OpenDecodeWindow?.Invoke(this, EventArgs.Empty);
    }

    internal void FindNext(string hexString)
    {
        FindNext(hexString, false);
    }

    public void SetLogData(Dictionary<string, LogData> data)
    {
        Data = data;
        logToolStripComboBox.Visible = true;
        logToolStripLabel.Visible = true;
    }

    private void LogToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var colour = Color.Black;
        LogData? logData = null;

        switch (logToolStripComboBox.SelectedItem?.ToString())
        {
            case "None":

                // Page.Refresh();
                break;

            case "Before":

                if (Data.TryGetValue("Before", out var before))
                {
                    logData = before;
                    colour = Color.Blue;
                }
                break;

            case "After":

                if (Data.TryGetValue("After", out var after))
                {
                    logData = after;
                    colour = Color.OrangeRed;
                }
                break;
        }
        if (logData != null && Page != null)
        {
            logData.MergeData(Page);

            var startPosition = Page.OffsetTable[logData.Slot] + logData.Offset;
            var endPosition = startPosition + logData.Data.Length;

            hexViewer.AddBlock(new BlockSelection { Colour = colour, StartPos = startPosition, EndPos = endPosition });

        }

        Refresh();
        LoadRecord(offsetTable.SelectedOffset);
    }
}