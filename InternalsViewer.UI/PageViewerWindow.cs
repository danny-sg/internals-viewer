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

namespace InternalsViewer.UI
{
    public partial class PageViewerWindow : UserControl
    {
        public event EventHandler<PageEventArgs> PageChanged;
        private readonly ProfessionalColorTable colourTable;
        private Page page;
        private ImageList keyImages;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageViewerWindow"/> class.
        /// </summary>
        public PageViewerWindow()
        {
            InitializeComponent();

            this.colourTable = new ProfessionalColorTable();
            this.colourTable.UseSystemColors = true;
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

        private void RefreshAllocationStatus(Database database, int extent)
        {
            int fileId = this.Page.PageAddress.FileId;

            //gamPictureBox.Image = database.Gam[fileId].Allocated(extent, fileId) ? unallocated : gamAllocated;
            //sGamPictureBox.Image = database.SGam[fileId].Allocated(extent, fileId) ? sGamAllocated : unallocated;
            //dcmPictureBox.Image = database.Dcm[fileId].Allocated(extent, fileId) ? dcmAllocated : unallocated;
            //bcmPictureBox.Image = database.Bcm[fileId].Allocated(extent, fileId) ? bcmAllocated : unallocated;
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
            if (pageAddress.FileId > 0)
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

                this.ConnectionString = connectionString;

                this.Page = new Page(this.ConnectionString, builder.InitialCatalog, pageAddress);
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
            this.LoadRecord(offsetTable.SelectedOffset);
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

                    break;

                case PageType.Lob3:
                case PageType.Lob4:

                    record = new BlobRecord(this.Page, offset);

                    break;
            }

            if (record != null)
            {
                List<Marker> markers = MarkerBuilder.BuildMarkers((Markable)record);

                this.hexViewer.AddMarkers(markers);

                this.markerKeyTable.SetMarkers(markers);

                this.hexViewer.ScrollToOffset(offset);
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
            LoadPage(this.ConnectionString,e.RowId);
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
    }
}
