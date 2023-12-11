using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Markers;
using System.Drawing;
using InternalsViewer.UI.Renderers;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.TransactionLog;
using Microsoft.Data.SqlClient;

#pragma warning disable CA1416

namespace InternalsViewer.UI;

public partial class PageViewerWindow : UserControl
{
    public event EventHandler<PageEventArgs> PageChanged;
    public event EventHandler OpenDecodeWindow;
    private readonly ProfessionalColorTable colourTable;
    private Page page;
    private readonly PfsRenderer pfsRenderer;
    private PfsByte pfsByte;
    int searchPosition = 0;
    private Dictionary<string, LogData> logData;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageViewerWindow"/> class.
    /// </summary>
    public PageViewerWindow()
    {
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

        OnPageChanged(this, new PageEventArgs(new RowIdentifier(Page.PageAddress, 0), false));
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

        gamPictureBox.Image = Page.AllocationStatus(PageType.Gam) ? unallocated : gamAllocated;
        sGamPictureBox.Image = Page.AllocationStatus(PageType.Sgam) ? sGamAllocated : unallocated;
        dcmPictureBox.Image = Page.AllocationStatus(PageType.Dcm) ? dcmAllocated : unallocated;
        bcmPictureBox.Image = Page.AllocationStatus(PageType.Bcm) ? bcmAllocated : unallocated;

        gamTextBox.Text = Allocation.AllocationPageAddress(pageAddress, PageType.Gam).ToString();
        sgamTextBox.Text = Allocation.AllocationPageAddress(pageAddress, PageType.Sgam).ToString();
        dcmTextBox.Text = Allocation.AllocationPageAddress(pageAddress, PageType.Dcm).ToString();
        bcmTextBox.Text = Allocation.AllocationPageAddress(pageAddress, PageType.Bcm).ToString();
        pfsTextBox.Text = Allocation.AllocationPageAddress(pageAddress, PageType.Pfs).ToString();

        pfsByte = Page.PfsStatus();

        pfsPanel.Invalidate();
    }

    /// <summary>
    /// Loads the page from a RID
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="rowIdentifier">The row identifier.</param>
    public void LoadPage(string connectionString, RowIdentifier rowIdentifier)
    {
        LoadPage(connectionString, rowIdentifier.PageAddress);

        offsetTable.SelectedSlot = rowIdentifier.SlotId;
    }

    /// <summary>
    /// Loads a page from a page address
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="pageAddress">The page address.</param>
    public void LoadPage(string connectionString, PageAddress pageAddress)
    {
        pageAddressToolStripStatusLabel.Text = string.Empty;
        offsetToolStripStatusLabel.Text = string.Empty;

        if (pageAddress.FileId > 0)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            ConnectionString = connectionString;

            Page = new Page(ConnectionString, builder.InitialCatalog, pageAddress);

            RefreshAllocationStatus(Page.PageAddress);

            pageToolStripTextBox.DatabaseId = Page.DatabaseId;

            serverToolStripStatusLabel.Text = builder.DataSource;
            dataaseToolStripStatusLabel.Text = builder.InitialCatalog;
        }
    }

    /// <summary>
    /// Handles the MouseClick event of the Page text boxes control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
    private void PageTextBox_MouseClick(object sender, MouseEventArgs e)
    {
        LoadPage(ConnectionString, PageAddress.Parse((sender as TextBox).Text));
    }

    /// <summary>
    /// Handles the SlotChanged event of the OffsetTable control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OffsetTable_SlotChanged(object sender, EventArgs e)
    {
        if (offsetTable.SelectedOffset > 0)
        {
            compressionInfoTable.SelectedStructure = CompressionInformation.CompressionInfoStructure.None;

            LoadRecord(offsetTable.SelectedOffset);
        }
    }

    /// <summary>
    /// Loads a record
    /// </summary>
    /// <param name="offset">The offset.</param>
    private void LoadRecord(ushort offset)
    {
        Record record = null;

        switch (Page.Header.PageType)
        {
            case PageType.Data:

                Structure tableStructure = new TableStructure(Page.Header.AllocationUnitId, Page.Database);

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

                Structure indexStructure = new IndexStructure(Page.Header.AllocationUnitId, Page.Database);

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
            var markers = MarkerBuilder.BuildMarkers((Markable)record);

            hexViewer.AddMarkers(markers);

            markerKeyTable.SetMarkers(markers);

            hexViewer.ScrollToOffset(offset);

            offsetTableToolStripTextBox.Text = string.Format("{0:0000}", offset);
        }
    }

    /// <summary>
    /// Sets record by slot
    /// </summary>
    /// <param name="slotId">The slot id.</param>
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
            else
            {
                currentOffset = (ushort)i;
            }
        }

        if (currentOffset > 0)
        {
            offsetTable.SelectedSlot = Page.OffsetTable.IndexOf(currentOffset);
        }
    }

    public void FindNext(string findHex, bool suppressContinue)
    {
        int endPos;

        var hexString = hexViewer.Text.Replace(" ", string.Empty).Replace("\n", string.Empty);

        var startPos = hexString.IndexOf(findHex, searchPosition + 1);

        if (startPos > 0)
        {
            if (startPos % 2 == 0)
            {
                endPos = startPos + findHex.Length - 1;
                searchPosition = endPos;

                hexViewer.SetSelection(startPos / 2, endPos / 2);
            }
            else if (!suppressContinue)
            {
                FindNext(findHex, true);
                return;
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
                LoadPage(ConnectionString, PageAddress.Parse(pageToolStripTextBox.Text));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
            }
        }
    }

    /// <summary>
    /// Handles the Click event of the PreviousToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void PreviousToolStripButton_Click(object sender, EventArgs e)
    {
        LoadPage(ConnectionString, new PageAddress(Page.PageAddress.FileId, Page.PageAddress.PageId - 1));
    }

    /// <summary>
    /// Handles the Click event of the NextToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void NextToolStripButton_Click(object sender, EventArgs e)
    {
        LoadPage(ConnectionString, new PageAddress(Page.PageAddress.FileId, Page.PageAddress.PageId + 1));
    }

    /// <summary>
    /// Handles the PageNavigated event of the MarkerKeyTable control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
    private void MarkerKeyTable_PageNavigated(object sender, PageEventArgs e)
    {
        LoadPage(ConnectionString, e.RowId);
    }

    /// <summary>
    /// Handles the SelectionChanged event of the MarkerKeyTable control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void MarkerKeyTable_SelectionChanged(object sender, EventArgs e)
    {
        hexViewer.SelectMarker(markerKeyTable.SelectedMarker);
    }

    /// <summary>
    /// Handles the SelectionClicked event of the MarkerKeyTable control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void MarkerKeyTable_SelectionClicked(object sender, EventArgs e)
    {
        hexViewer.HideToolTip();
    }

    /// <summary>
    /// Handles the OffsetOver event of the HexViewer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.UI.OffsetEventArgs"/> instance containing the event data.</param>
    private void HexViewer_OffsetOver(object sender, OffsetEventArgs e)
    {
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

        offsetToolStripStatusLabel.Text = string.Format("Offset: {0:0000}", e.Offset);
    }

    private void DisplayCompressionInfoStructure(CompressionInformation.CompressionInfoStructure compressionInfoStructure)
    {
        var markers = new List<Marker>();

        switch (compressionInfoStructure)
        {
            case CompressionInformation.CompressionInfoStructure.Anchor:

                if (Page.CompressionInformation.AnchorRecord != null)
                {
                    markers = MarkerBuilder.BuildMarkers(Page.CompressionInformation.AnchorRecord);
                }

                break;

            case CompressionInformation.CompressionInfoStructure.Dictionary:

                if (Page.CompressionInformation.HasDictionary)
                {
                    markers = MarkerBuilder.BuildMarkers(Page.CompressionInformation.CompressionDictionary);
                }

                break;

            case CompressionInformation.CompressionInfoStructure.Header:

                markers = MarkerBuilder.BuildMarkers(Page?.CompressionInformation);
                break;
        }

        hexViewer.AddMarkers(markers);

        markerKeyTable.SetMarkers(markers);
    }


    /// <summary>
    /// Handles the PageOver event of the AllocationViewer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
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

    /// <summary>
    /// Handles the PageClicked event of the AllocationViewer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
    private void AllocationViewer_PageClicked(object sender, PageEventArgs e)
    {
        LoadPage(ConnectionString, e.Address);
    }

    /// <summary>
    /// Handles the KeyDown event of the offsetTableToolStripTextBox control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Forms.KeyEventArgs"/> instance containing the event data.</param>
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

    /// <summary>
    /// Handles the Paint event of the PfsPanel control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
    private void PfsPanel_Paint(object sender, PaintEventArgs e)
    {
        if (pfsByte != null)
        {
            pfsRenderer.DrawPfsPage(e.Graphics, new Rectangle(0, 0, 32, 32), pfsByte);
        }
    }

    /// <summary>
    /// Handles the RecordFind event of the HexViewer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.UI.OffsetEventArgs"/> instance containing the event data.</param>
    private void HexViewer_RecordFind(object sender, OffsetEventArgs e)
    {
        int offset = e.Offset;

        FindRecord(offset);
    }

    /// <summary>
    /// Handles the OffsetSet event of the HexViewer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.UI.OffsetEventArgs"/> instance containing the event data.</param>
    private void HexViewer_OffsetSet(object sender, OffsetEventArgs e)
    {
        LoadRecord(e.Offset);
    }

    /// <summary>
    /// Raises the <see cref="E:System.Windows.Forms.Control.Paint"/> event.
    /// </summary>
    /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"/> that contains the event data.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var brush = new LinearGradientBrush(ClientRectangle,
            colourTable.ToolStripGradientBegin,
            colourTable.ToolStripGradientEnd,
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    /// <summary>
    /// Called when the current page changes
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
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

    /// <summary>
    /// Gets or sets the current Page.
    /// </summary>
    /// <value>The page.</value>
    public Page Page
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

    private void CompressionInfoTable_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        offsetTable.SelectedSlot = -1;

        DisplayCompressionInfoStructure(compressionInfoTable.SelectedStructure);
    }

    private void EncodeAndFindToolStripButton_Click(object sender, EventArgs e)
    {
        OpenDecodeWindow(this, EventArgs.Empty);
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
        int startPos;
        int endPos;
        var colour = Color.Black;
        LogData logData = null;

        switch (logToolStripComboBox.SelectedItem.ToString())
        {
            case "None":

                Page.Refresh();
                break;

            case "Before":

                if (this.logData.ContainsKey("Before"))
                {
                    logData = this.logData["Before"];
                    colour = Color.Blue;
                }
                break;

            case "After":

                if (this.logData.ContainsKey("After"))
                {
                    logData = this.logData["After"];
                    colour = Color.OrangeRed;
                }
                break;
        }
        if (logData != null)
        {
            logData.MergeData(Page);

            startPos = Page.OffsetTable[logData.Slot] + logData.Offset;
            endPos = startPos + logData.Data.Length;

            hexViewer.AddBlock(new BlockSelection() { Colour = colour, StartPos = startPos, EndPos = endPos });

        }

        Refresh();
        LoadRecord(offsetTable.SelectedOffset);
    }
}