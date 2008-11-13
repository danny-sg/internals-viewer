using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Structures;
using InternalsViewer.UI.Markers;
using System.Drawing;
using InternalsViewer.UI.Renderers;

namespace InternalsViewer.UI
{
    public partial class PageViewerWindow : UserControl
    {
        public event EventHandler<PageEventArgs> PageChanged;
        private readonly ProfessionalColorTable colourTable;
        private Page page;
        private ImageList keyImages;
        private readonly PfsRenderer pfsRenderer;
        private PfsByte pfsByte;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageViewerWindow"/> class.
        /// </summary>
        public PageViewerWindow()
        {
            InitializeComponent();

            this.colourTable = new ProfessionalColorTable();
            this.colourTable.UseSystemColors = true;

            this.pfsRenderer = new PfsRenderer(new Rectangle(1, 1, 34, 34), Color.White, SystemColors.ControlDark);
        }

        /// <summary>
        /// Create the key images for the allocation pages
        /// </summary>
        private void CreateKeyImages()
        {
            keyImages = new ImageList();
        }

        /// <summary>
        /// Loads the page.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="database">The database.</param>
        /// <param name="rowIdentifier">The row identifier.</param>
        public void LoadPage(string connectionString, string database, RowIdentifier rowIdentifier)
        {
            pageAddressToolStripStatusLabel.Text = string.Empty;
            offsetToolStripStatusLabel.Text = string.Empty;

            this.Page = new Page(connectionString, database, rowIdentifier.PageAddress);

            this.SetSlot(rowIdentifier.SlotId);
        }

        /// <summary>
        /// Refreshes the current page in the page viewer
        /// </summary>
        /// <param name="page">The page.</param>
        private void RefreshPage(Page page)
        {
            this.pageToolStripTextBox.Text = page.PageAddress.ToString();
            this.hexViewer.Page = this.Page;
            this.pageBindingSource.DataSource = this.Page.Header;
            this.offsetTable.Page = this.Page;

            this.OnPageChanged(this, new PageEventArgs(new RowIdentifier(this.Page.PageAddress, 0), false));
        }

        private void RefreshAllocationStatus(PageAddress pageAddress)
        {
            Image unallocated = ExtentColour.KeyImage(Color.Gainsboro);
            Image gamAllocated = ExtentColour.KeyImage(Color.FromArgb(172, 186, 214));
            Image sGamAllocated = ExtentColour.KeyImage(Color.FromArgb(168, 204, 162));
            Image dcmAllocated = ExtentColour.KeyImage(Color.FromArgb(120, 150, 150));
            Image bcmAllocated = ExtentColour.KeyImage(Color.FromArgb(150, 120, 150));

            gamPictureBox.Image = page.AllocationStatus(AllocationPageType.Gam) ? unallocated : gamAllocated;
            sGamPictureBox.Image = page.AllocationStatus(AllocationPageType.Sgam) ? sGamAllocated : unallocated;
            dcmPictureBox.Image = page.AllocationStatus(AllocationPageType.Dcm) ? dcmAllocated : unallocated;
            bcmPictureBox.Image = page.AllocationStatus(AllocationPageType.Bcm) ? bcmAllocated : unallocated;

            gamTextBox.Text = Header.AllocationPageAddress(pageAddress, AllocationPageType.Gam).ToString();
            sgamTextBox.Text = Header.AllocationPageAddress(pageAddress, AllocationPageType.Sgam).ToString();
            dcmTextBox.Text = Header.AllocationPageAddress(pageAddress, AllocationPageType.Dcm).ToString();
            bcmTextBox.Text = Header.AllocationPageAddress(pageAddress, AllocationPageType.Bcm).ToString();

            //      this.pfsByte = page.Database.Pfs[fileId].PagePfsByte(pageAddress.PageId);
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.Paint"/> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"/> that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle,
                                                                       colourTable.ToolStripGradientBegin,
                                                                       colourTable.ToolStripGradientEnd,
                                                                       LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        /// <summary>
        /// Called when the current page changes
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        internal virtual void OnPageChanged(object sender, PageEventArgs e)
        {
            if (this.PageChanged != null)
            {
                this.PageChanged(sender, e);
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
                    this.LoadPage(this.ConnectionString, PageAddress.Parse(pageToolStripTextBox.Text));
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
            this.LoadPage(this.ConnectionString, new PageAddress(this.Page.PageAddress.FileId, page.PageAddress.PageId - 1));
        }

        /// <summary>
        /// Handles the Click event of the NextToolStripButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void NextToolStripButton_Click(object sender, EventArgs e)
        {
            this.LoadPage(this.ConnectionString, new PageAddress(this.Page.PageAddress.FileId, page.PageAddress.PageId + 1));
        }

        public void LoadPage(string connectionString, RowIdentifier rowIdentifier)
        {
            this.LoadPage(connectionString, rowIdentifier.PageAddress);

            this.offsetTable.SelectedSlot = rowIdentifier.SlotId;
        }

        /// <summary>
        /// Loads a page.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="pageAddress">The page address.</param>
        public void LoadPage(string connectionString, PageAddress pageAddress)
        {
            pageAddressToolStripStatusLabel.Text = string.Empty;
            offsetToolStripStatusLabel.Text = string.Empty;

            if (pageAddress.FileId > 0)
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

                this.ConnectionString = connectionString;

                this.Page = new Page(this.ConnectionString, builder.InitialCatalog, pageAddress);

                RefreshAllocationStatus(this.Page.PageAddress);
            }
        }

        /// <summary>
        /// Handles the MouseClick event of the nextPageTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void NextPageTextBox_MouseClick(object sender, MouseEventArgs e)
        {
            this.LoadPage(this.ConnectionString, this.Page.Header.NextPage);
        }

        /// <summary>
        /// Handles the MouseClick event of the PreviousPageTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void PreviousPageTextBox_MouseClick(object sender, MouseEventArgs e)
        {
            this.LoadPage(this.ConnectionString, this.Page.Header.PreviousPage);
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
                this.LoadRecord(offsetTable.SelectedOffset);
            }
        }

        /// <summary>
        /// Loads a record
        /// </summary>
        /// <param name="offset">The offset.</param>
        private void LoadRecord(ushort offset)
        {
            Record record = null;

            switch (this.Page.Header.PageType)
            {
                case PageType.Data:

                    Structure tableStructure = new TableStructure(this.Page.Header.AllocationUnitId, this.Page.Database);

                    record = new DataRecord(this.Page, offset, tableStructure);
                    allocationViewer.Visible = false;
                    markerKeyTable.Visible = true;
                    break;

                case PageType.Index:

                    Structure indexStructure = new IndexStructure(this.Page.Header.AllocationUnitId, this.Page.Database);

                    record = new IndexRecord(this.Page, offset, indexStructure);

                    allocationViewer.Visible = false;
                    markerKeyTable.Visible = true;
                    break;

                case PageType.Iam:
                case PageType.Gam:
                case PageType.Sgam:
                case PageType.Bcm:
                case PageType.Dcm:

                    allocationViewer.SetAllocationPage(this.Page.Header.PageAddress,
                                                       this.Page.Database.Name,
                                                       this.ConnectionString,
                                                      (this.Page.Header.PageType == PageType.Iam));

                    markerKeyTable.Visible = false;
                    allocationViewer.Visible = true;
                    break;

                case PageType.Lob3:
                case PageType.Lob4:

                    record = new BlobRecord(this.Page, offset);

                    allocationViewer.Visible = false;
                    markerKeyTable.Visible = true;
                    break;
            }

            if (record != null)
            {
                List<Marker> markers = MarkerBuilder.BuildMarkers((Markable)record);

                this.hexViewer.AddMarkers(markers);

                this.markerKeyTable.SetMarkers(markers);

                this.hexViewer.ScrollToOffset(offset);

                this.offsetTableToolStripTextBox.Text = string.Format("{0:0000}", offset);
            }
        }

        /// <summary>
        /// Sets record by slot
        /// </summary>
        /// <param name="slotId">The slot id.</param>
        public void SetSlot(int slotId)
        {
            this.offsetTable.SelectedSlot = slotId;
        }

        private void MarkerKeyTable_PageNavigated(object sender, PageEventArgs e)
        {
            LoadPage(this.ConnectionString, e.RowId);
        }

        /// <summary>
        /// Handles the SelectionChanged event of the MarkerKeyTable control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void MarkerKeyTable_SelectionChanged(object sender, EventArgs e)
        {
            this.hexViewer.SelectMarker(markerKeyTable.SelectedMarker);
        }

        /// <summary>
        /// Handles the SelectionClicked event of the MarkerKeyTable control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void MarkerKeyTable_SelectionClicked(object sender, EventArgs e)
        {
            this.hexViewer.HideToolTip();
        }

        private void HexViewer_OffsetOver(object sender, OffsetEventArgs e)
        {
            switch (this.Page.Header.PageType)
            {
                case PageType.Bcm:
                case PageType.Dcm:
                case PageType.Gam:
                case PageType.Iam:
                case PageType.Sgam:

                    if (e.Offset >= AllocationPage.AllocationArrayOffset)
                    {
                        int startExtent = (e.Offset - AllocationPage.AllocationArrayOffset) * 8;
                        int endExtent = startExtent + 7;

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

        private void AllocationViewer_PageOver(object sender, PageEventArgs e)
        {
            pageAddressToolStripStatusLabel.Text = e.Address.ToString();
        }

        private void AllocationViewer_PageClicked(object sender, PageEventArgs e)
        {
            this.LoadPage(this.ConnectionString, e.Address);
        }

        private void offsetTableToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                ushort offset;

                if (ushort.TryParse(offsetTableToolStripTextBox.Text, out offset) && offset < Page.Size && offset > 0)
                {
                    this.LoadRecord(offset);
                }
            }
        }

        private void PfsPanel_Paint(object sender, PaintEventArgs e)
        {
            if (this.pfsByte != null)
            {
                this.pfsRenderer.DrawPfsPage(e.Graphics, new Rectangle(0, 0, 32, 32), pfsByte);
            }
        }

        private void HexViewer_RecordFind(object sender, OffsetEventArgs e)
        {
            int offset = e.Offset;

            FindRecord(offset);
        }

        private void FindRecord(int offset)
        {
            List<ushort> sortedOffsetTable = new List<ushort>(this.Page.OffsetTable.ToArray());

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
                this.offsetTable.SelectedSlot = Page.OffsetTable.IndexOf(currentOffset);
            }
        }

        /// <summary>
        /// Gets or sets the current Page.
        /// </summary>
        /// <value>The page.</value>
        public Page Page
        {
            get
            {
                return this.page;
            }
            set
            {
                this.page = value;

                if (this.page != null)
                {
                    this.RefreshPage(this.Page);
                }
            }
        }

        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        /// <value>The connection string.</value>
        public string ConnectionString { get; set; }

        private void hexViewer_RecordFind(object sender, OffsetEventArgs e)
        {

        }

        private void HexViewer_OffsetSet(object sender, OffsetEventArgs e)
        {
            this.LoadRecord(e.Offset);
        }

    }
}
