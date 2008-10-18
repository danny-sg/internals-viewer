using System;
using System.Data.SqlClient;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI
{
    public partial class PageViewerWindow : UserControl
    {
        public event EventHandler<PageEventArgs> PageChanged;
        private readonly ProfessionalColorTable colourTable;
        private Page page;
        private ImageList keyImages;
        private string connectionString;

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
        /// Loads the page into the viewer
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="database">The database name.</param>
        /// <param name="pageAddress">The page address.</param>
        public void LoadPage(string connectionString, string database, PageAddress pageAddress)
        {
            this.Page = new Page(connectionString, database, pageAddress);
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
        /// Gets or sets the connection string.
        /// </summary>
        /// <value>The connection string.</value>
        public string ConnectionString
        {
            get { return connectionString; }
            set { connectionString = value; }
        }
    }
}
