using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.TransactionLog;
using InternalsViewer.UI.Markers;
using InternalsViewer.UI.Renderers;

#pragma warning disable CA1416

namespace InternalsViewer.UI;

public partial class PageViewerWindow : UserControl
{
    public IPageService PageService { get; }

    public event EventHandler<PageEventArgs>? PageChanged;

    public event EventHandler OpenDecodeWindow;

    private readonly ProfessionalColorTable colourTable;

    private Page page;

    private readonly PfsRenderer pfsRenderer;

    private PfsByte pfsByte;

    int searchPosition;

    private Dictionary<string, LogData> logData;

    public Database Database { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PageViewerWindow"/> class.
    /// </summary>
    public PageViewerWindow(IPageService pageService)
    {
        PageService = pageService;

        InitializeComponent();

        colourTable = new ProfessionalColorTable();
        colourTable.UseSystemColors = true;

        pfsRenderer = new PfsRenderer(new Rectangle(1, 1, 34, 34), Color.White, SystemColors.ControlDark);
    }

    /// <summary>
    /// Create the key images for the allocation pages
    /// </summary>
    private void CreateKeyImages()
    {
        new ImageList();
    }

    /// <summary>
    /// Refreshes the current page in the page viewer
    /// </summary>
    /// <param name="page">The page.</param>
    private void RefreshPage(Page page)
    {
        pageToolStripTextBox.Text = page.PageAddress.ToString() ?? string.Empty;
        hexViewer.Page = Page;
        pageBindingSource.DataSource = Page.Header;
        offsetTable.Page = Page;

        if (page.CompressionType == CompressionType.Page && !Page.OffsetTable.Contains(96))
        {
            compressionInfoPanel.Visible = true;
        }
        else
        {
            compressionInfoPanel.Visible = false;
        }

        OnPageChanged(this, new PageEventArgs(page.PageAddress, false));
    }

    /// <summary>
    /// Refreshes the allocation status from the various allocation pages
    /// </summary>
    /// <param name="pageAddress">The page address.</param>
    private void RefreshAllocationStatus(PageAddress pageAddress)
    {
        Image unallocated = ExtentColour.KeyImage(Color.Gainsboro);
        Image gamAllocated = ExtentColour.KeyImage(Color.FromArgb(172, 186, 214));
        Image sGamAllocated = ExtentColour.KeyImage(Color.FromArgb(168, 204, 162));
        Image dcmAllocated = ExtentColour.KeyImage(Color.FromArgb(120, 150, 150));
        Image bcmAllocated = ExtentColour.KeyImage(Color.FromArgb(150, 120, 150));

        var fileId = pageAddress.FileId;
        gamPictureBox.Image = Page.Database.Gam[fileId].IsAllocated(pageAddress.Extent, fileId) ? unallocated : gamAllocated;
        sGamPictureBox.Image = Page.Database.SGam[fileId].IsAllocated(pageAddress.Extent, fileId) ? unallocated : sGamAllocated;
        dcmPictureBox.Image = Page.Database.Dcm[fileId].IsAllocated(pageAddress.Extent, fileId) ? unallocated : dcmAllocated;
        bcmPictureBox.Image = Page.Database.Bcm[fileId].IsAllocated(pageAddress.Extent, fileId) ? unallocated : bcmAllocated;

        //gamTextBox.Text = page.AllocationStatus.GamPageAddress.ToString();
        //sgamTextBox.Text = page.AllocationStatus.SgamPageAddress.ToString();
        //dcmTextBox.Text = page.AllocationStatus.DcmPageAddress.ToString();
        //bcmTextBox.Text = page.AllocationStatus.BcmPageAddress.ToString();
        //pfsTextBox.Text = page.AllocationStatus.PfsPageAddress.ToString();

        //pfsByte = Page.PfsStatus();

        pfsPanel.Invalidate();
    }

    public void LoadPage(RowIdentifier rowIdentifier)
    {
        // LoadPage(connectionString, rowIdentifier.PageAddress);

        offsetTable.SelectedSlot = rowIdentifier.SlotId;
    }

    public async void LoadPage(PageAddress pageAddress)
    {
        pageAddressToolStripStatusLabel.Text = string.Empty;
        offsetToolStripStatusLabel.Text = string.Empty;

        if (pageAddress.FileId > 0)
        {
            Page = await PageService.Load<Page>(Database, pageAddress);
            //Page = new Page(ConnectionString, builder.InitialCatalog, pageAddress);

            RefreshAllocationStatus(Page.PageAddress);

            pageToolStripTextBox.DatabaseId = Page.Database!.DatabaseId;

            //serverToolStripStatusLabel.Text = builder.DataSource;
            //dataaseToolStripStatusLabel.Text = builder.InitialCatalog;
        }
    }

    private void PageTextBox_MouseClick(object sender, MouseEventArgs e)
    {
        LoadPage(PageAddressParser.Parse(((TextBox)sender).Text));
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
        if (Page == null)
        {
            return;
        }

        Record? record = null;

        switch (Page.Header.PageType)
        {
            case PageType.Data:

                Structure tableStructure = new TableStructure(Page.Header.AllocationUnitId);

                if (Page.CompressionType == CompressionType.None)
                {
                    record = new DataRecord(Page, offset, tableStructure);
                }
                else
                {
                    record = new CompressedDataRecord(Page, offset, tableStructure);
                }

                allocationViewer.Visible = false;
                markerKeyTable.Visible = true;
                break;

            case PageType.Index:

                Structure indexStructure = new IndexStructure(Page.Header.AllocationUnitId);

                record = new IndexRecord(Page, offset, indexStructure);

                allocationViewer.Visible = false;
                markerKeyTable.Visible = true;
                break;

            case PageType.Iam:
            case PageType.Gam:
            case PageType.Sgam:
            case PageType.Bcm:
            case PageType.Dcm:

                allocationViewer.SetAllocationPage(Page.Header.PageAddress,
                    Page.Database.Name,
                    ConnectionString,
                    (Page.Header.PageType == PageType.Iam));

                markerKeyTable.Visible = false;
                allocationViewer.Visible = true;
                break;

            case PageType.Pfs:

                allocationViewer.SetPfsPage(Page.Header.PageAddress,
                    Page.Database.Name,
                    ConnectionString);

                markerKeyTable.Visible = false;
                allocationViewer.Visible = true;
                break;

            case PageType.Lob3:
            case PageType.Lob4:

                record = new BlobRecord(Page, offset);

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

        var startPosition = hexString.IndexOf(findHex, searchPosition + 1, StringComparison.Ordinal);

        if (startPosition > 0)
        {
            if (startPosition % 2 == 0)
            {
                var endPosition = startPosition + findHex.Length - 1;
                searchPosition = endPosition;

                hexViewer.SetSelection(startPosition / 2, endPosition / 2);
            }
            else if (!suppressContinue)
            {
                FindNext(findHex, true);
            }
        }
        else
        {
            searchPosition = 0;
            MessageBox.Show("Search hex not found", "Find", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }
    /// <summary>
    /// Handles the KeyDown event of the PageToolStripTextBox control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Forms.KeyEventArgs"/> instance containing the event data.</param>
    private void PageToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Return)
        {
            try
            {
                LoadPage(PageAddressParser.Parse(pageToolStripTextBox.Text));
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
            }
        }
    }

    private void PreviousToolStripButton_Click(object sender, EventArgs e)
    {
        LoadPage(new PageAddress(Page.PageAddress.FileId, Page.PageAddress.PageId - 1));
    }

    private void NextToolStripButton_Click(object sender, EventArgs e)
    {
        LoadPage(new PageAddress(Page.PageAddress.FileId, Page.PageAddress.PageId + 1));
    }

    private void MarkerKeyTable_PageNavigated(object sender, PageEventArgs e)
    {
        LoadPage(e.RowId);
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

        switch (Page.Header.PageType)
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

                    markerDescriptionToolStripStatusLabel.Text = string.Format("Extents {0} - {1} | Pages {2} - {3}",
                        startExtent,
                        endExtent,
                        startExtent * 8,
                        (endExtent * 8) + 7);
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
        var markers = new List<Marker>();

        switch (compressionInfoStructure)
        {
            case CompressionInfoStructure.Anchor:

                if (Page.CompressionInfo?.AnchorRecord != null)
                {
                    markers = MarkerBuilder.BuildMarkers(Page.CompressionInfo.AnchorRecord);
                }

                break;

            case CompressionInfoStructure.Dictionary:

                if (Page.CompressionInfo is { HasDictionary: true })
                {
                    markers = MarkerBuilder.BuildMarkers(Page.CompressionInfo.CompressionDictionary);
                }

                break;

            case CompressionInfoStructure.Header:

                markers = MarkerBuilder.BuildMarkers(Page?.CompressionInfo);
                break;
        }

        hexViewer.AddMarkers(markers);

        markerKeyTable.SetMarkers(markers);
    }

    private void AllocationViewer_PageOver(object sender, PageEventArgs e)
    {
        if (Page.Header.PageType == PageType.Pfs)
        {
            pageAddressToolStripStatusLabel.Text = e.Address.ToString(); //+ " " + this.allocationViewer.allocationContainer.PagePfsByte(e.Address).ToString();
        }
        else
        {
            pageAddressToolStripStatusLabel.Text = e.Address.ToString();
        }
    }

    private void AllocationViewer_PageClicked(object sender, PageEventArgs e)
    {
        LoadPage(e.Address);
    }

    private void OffsetTableToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Return)
        {
            ushort offset;

            if (ushort.TryParse(offsetTableToolStripTextBox.Text, out offset) && offset < Page.Size && offset > 0)
            {
                LoadRecord(offset);
            }
        }
    }

    private void PfsPanel_Paint(object sender, PaintEventArgs e)
    {
        if (pfsByte != null)
        {
            pfsRenderer.DrawPfsPage(e.Graphics, new Rectangle(0, 0, 32, 32), pfsByte);
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
        if (PageChanged != null)
        {
            PageChanged(sender, e);
        }
    }

    internal virtual void OnOpenDecodeWindow(object sender, PageEventArgs e)
    {
        if (OpenDecodeWindow != null)
        {
            OpenDecodeWindow(sender, e);
        }
    }

    public Page? Page
    {
        get => page;
        set
        {
            page = value;

            if (page != null)
            {
                RefreshPage(Page);
            }
        }
    }

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    /// <value>The connection string.</value>
    public string ConnectionString { get; set; }

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

    public void SetLogData(Dictionary<string, LogData> logData)
    {
        this.logData = logData;
        logToolStripComboBox.Visible = true;
        logToolStripLabel.Visible = true;
    }

    private void LogToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var colour = Color.Black;
        LogData logData = null;

        switch (logToolStripComboBox.SelectedItem?.ToString())
        {
            case "None":

                // Page.Refresh();
                break;

            case "Before":

                if (this.logData.TryGetValue("Before", out var before))
                {
                    logData = before;
                    colour = Color.Blue;
                }
                break;

            case "After":

                if (this.logData.TryGetValue("After", out var after))
                {
                    logData = after;
                    colour = Color.OrangeRed;
                }
                break;
        }
        if (logData != null)
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