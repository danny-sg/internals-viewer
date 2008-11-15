using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Renderers;

namespace InternalsViewer.UI.Allocations
{
    /// <summary>
    /// Allocation Map used to display allocation layers for files
    /// </summary>
    public class AllocationMap : Panel, IDisposable
    {
        public static Size Large = new Size(256, 32);
        public static Size Medium = new Size(96, 12);
        public static Size Small = new Size(64, 8);

        private readonly Color defaultPageBorderColour = Color.White;
        private Color borderColour = Color.Gainsboro;
        private int extentCount;
        private int extentsHorizontal;
        private int extentsRemaining;
        private int extentsVertical;
        private int visibleExtents;
        private int windowPosition;
        private Pfs pfs;
        private DatabaseFile file;
        private int fileId;
        private bool includeIam;
        private List<AllocationLayer> mapLayers;
        private Size extentSize;
        private MapMode mode;
        private readonly VScrollBar scrollBar;
        private int selectedPage = -1;
        private PageAddress startPage = new PageAddress(1, 0);
        public event EventHandler<PageEventArgs> PageClicked;
        public event EventHandler RangeSelected;
        public event EventHandler<PageEventArgs> PageOver;
        public event EventHandler WindowPositionChanged;
        private readonly PageExtentRenderer pageExtentRenderer;
        private readonly PfsRenderer pfsRenderer;
        private bool holding;
        private string holdingMessage;

        private int selectionStartExtent = -1;
        private int selectionEndExtent = -1;
        private int provisionalEndExtent;
        private BackgroundWorker imageBufferBackgroundWorker = new BackgroundWorker();

        private Pen backgroundLine = new Pen(Color.FromArgb(242, 242, 242), 2);
        private LinearGradientBrush backgroundBrush;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllocationMap"/> class.
        /// </summary>
        public AllocationMap()
        {
            SuspendLayout();

            this.BackColor = Color.White;

            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.DoubleBuffer, true);

            this.Padding = new Padding(1);
            this.scrollBar = new VScrollBar();
            this.scrollBar.Enabled = false;
            this.scrollBar.Dock = DockStyle.Right;

            Controls.Add(this.scrollBar);

            ResumeLayout(false);

            this.MouseClick += this.AllocationMapPanel_MouseClick;
            this.scrollBar.ValueChanged += this.ScrollBar_ValueChanged;
            this.MouseMove += this.AllocationMapPanel_MouseMove;
            this.Resize += new EventHandler(AllocationMap_Resize);
            this.extentSize = AllocationMap.Small;

            this.imageBufferBackgroundWorker.DoWork += this.ImageBufferBackgroundWorker_DoWork;
            this.imageBufferBackgroundWorker.RunWorkerCompleted += this.ImageBufferBackgroundWorker_RunWorkerCompleted;
            this.imageBufferBackgroundWorker.ProgressChanged += this.ImageBufferBackgroundWorker_ProgressChanged;

            this.imageBufferBackgroundWorker.WorkerReportsProgress = true;
            this.imageBufferBackgroundWorker.WorkerSupportsCancellation = true;

            this.pageExtentRenderer = new PageExtentRenderer(Color.WhiteSmoke, Color.FromArgb(234, 234, 234));
            pfsRenderer = new PfsRenderer(new Rectangle(0, 0, Large.Height, Large.Width / 8), Color.WhiteSmoke);

            this.pageExtentRenderer.CreateBrushesAndPens(this.ExtentSize);

            this.backgroundBrush = new LinearGradientBrush(this.ClientRectangle, SystemColors.Control, SystemColors.ControlLightLight, LinearGradientMode.Horizontal);
            this.backgroundBrush.WrapMode = WrapMode.TileFlipX;

            this.mapLayers = new List<AllocationLayer>();
        }

        void AllocationMap_Resize(object sender, EventArgs e)
        {
            this.backgroundBrush.ResetTransform();
            this.backgroundBrush.ScaleTransform(this.Bounds.Height / this.backgroundBrush.Rectangle.Height,
                                                this.Bounds.Width / this.backgroundBrush.Rectangle.Width,
                                                MatrixOrder.Append);
        }

        /// <summary>
        /// Begins the async full map render
        /// </summary>
        public void ShowFullMap()
        {
            this.HoldingMessage = "Rendering...";

            if (this.imageBufferBackgroundWorker.IsBusy)
            {
                this.imageBufferBackgroundWorker.CancelAsync();
            }

            while (this.imageBufferBackgroundWorker.IsBusy)
            {
                Application.DoEvents();
            }

            this.imageBufferBackgroundWorker.RunWorkerAsync();
            this.BackgroundImageLayout = ImageLayout.Stretch;
        }

        /// <summary>
        /// Draws the pages in the single page slots for the allocations the map displays
        /// </summary>
        /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
        private void DrawSinglePages(PaintEventArgs e)
        {
            this.pageExtentRenderer.ResizePageBrush(this.ExtentSize);

            foreach (AllocationLayer layer in this.mapLayers)
            {
                if (layer.Visible)
                {
                    Color pageColour;

                    if (layer.UseDefaultSinglePageColour)
                    {
                        pageColour = Color.Salmon;
                    }
                    else
                    {
                        pageColour = layer.Colour;
                    }

                    this.pageExtentRenderer.SetExtentBrushColour(pageColour, ExtentColour.LightBackgroundColour(pageColour));

                    if (layer.UseBorderColour)
                    {
                        this.pageExtentRenderer.PageBorderColour = layer.BorderColour;
                    }
                    else
                    {
                        this.pageExtentRenderer.PageBorderColour = this.defaultPageBorderColour;
                    }

                    foreach (Allocation allocation in layer.Allocations)
                    {
                        foreach (PageAddress address in allocation.SinglePageSlots)
                        {
                            if (address.FileId == this.FileId && address.PageId != 0 && this.CheckPageVisible(address.PageId))
                            {
                                this.pageExtentRenderer.DrawPage(e.Graphics,
                                                                 this.PagePosition(address.PageId - (this.WindowPosition * 8)),
                                                                 layer.LayerType);
                            }
                        }

                        if (this.includeIam)
                        {
                            foreach (AllocationPage page in allocation.Pages)
                            {
                                if (this.CheckPageVisible(page.PageAddress.PageId))
                                {
                                    this.pageExtentRenderer.DrawPage(e.Graphics,
                                                                     this.PagePosition(page.PageAddress.PageId - (this.WindowPosition * 8)),
                                                                     AllocationLayerType.Standard);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws the extents for each allocation layer
        /// </summary>
        /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
        private void DrawExtents(PaintEventArgs e)
        {
            this.pageExtentRenderer.ResizeExtentBrush(this.ExtentSize);

            for (int extent = this.windowPosition;
                 extent < this.extentCount && extent < (this.visibleExtents + this.windowPosition);
                 extent++)
            {
                foreach (AllocationLayer layer in this.mapLayers)
                {
                    if (layer.Visible && !layer.SingleSlotsOnly)
                    {
                        foreach (Allocation chain in layer.Allocations)
                        {
                            int targetExtent = extent + (this.startPage.PageId / 8);

                            if (Allocation.CheckAllocationStatus(targetExtent, this.fileId, layer.Invert, chain))
                            {
                                this.pageExtentRenderer.SetExtentBrushColour(layer.Colour,
                                                                             ExtentColour.BackgroundColour(layer.Colour));

                                this.pageExtentRenderer.DrawExtent(e.Graphics, this.ExtentPosition(extent - this.WindowPosition));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks the page is visible on the map
        /// </summary>
        /// <param name="pageId">The page Id.</param>
        /// <returns></returns>
        private bool CheckPageVisible(int pageId)
        {
            return pageId >= (this.windowPosition * 8) && pageId <= ((this.visibleExtents + this.windowPosition) * 8);
        }

        /// <summary>
        /// Get Rectange for a particular extent
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns></returns>
        private Rectangle ExtentPosition(int extent)
        {
            if (this.extentsHorizontal > 1)
            {
                return new Rectangle((extent * this.extentSize.Width) % (this.extentsHorizontal * this.extentSize.Width),
                                     (int)Math.Floor((decimal)extent / this.extentsHorizontal) * this.extentSize.Height,
                                     this.extentSize.Width,
                                     this.extentSize.Height);
            }
            else
            {
                return new Rectangle(0, 0, this.extentSize.Width, this.extentSize.Height);
            }
        }

        /// <summary>
        /// Get the position for a particular page
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns></returns>
        private Rectangle PagePosition(int page)
        {
            int pageWidth = this.extentSize.Width / 8;

            if (page != 0)
            {
                return new Rectangle((page * pageWidth) % ((this.extentsHorizontal * 8) * pageWidth),
                                     (int)Math.Floor((decimal)page / (this.extentsHorizontal * 8)) * this.extentSize.Height,
                                     pageWidth,
                                     this.extentSize.Height);
            }
            else
            {
                return new Rectangle(0, 0, pageWidth, this.extentSize.Height);
            }
        }

        /// <summary>
        /// Get the extent at a particular x and y position
        /// </summary>
        /// <param name="x">The x co-ordinate</param>
        /// <param name="y">The y co-ordinate</param>
        private int ExtentPosition(int x, int y)
        {
            return 1 + (y / this.extentSize.Height * this.extentsHorizontal) + (x / this.extentSize.Width);
        }

        /// <summary>
        /// Get the page at a particular x and y position
        /// </summary>
        /// <param name="x">The x co-ordinate</param>
        /// <param name="y">The y co-ordinate</param>
        private int PagePosition(int x, int y)
        {
            return (y / this.extentSize.Height * (this.extentsHorizontal * 8)) + (x / (this.extentSize.Width / 8));
        }

        /// <summary>
        /// Calculates the number (horizontal and vertical) of the visible extents.
        /// </summary>
        internal void CalculateVisibleExtents()
        {
            this.extentsHorizontal = (int)Math.Floor((decimal)(Width - this.scrollBar.Width) / this.extentSize.Width);
            this.extentsVertical = (int)Math.Ceiling((decimal)Height / this.extentSize.Height);

            if (this.extentsHorizontal == 0 | this.extentsVertical == 0 | this.extentCount == 0)
            {
                return;
            }

            this.extentsRemaining = this.extentCount - (this.extentsHorizontal * this.extentsVertical);

            this.scrollBar.SmallChange = this.extentsHorizontal;
            this.scrollBar.LargeChange = (this.extentsVertical - 1) * this.extentsHorizontal;

            if (this.extentsHorizontal == 0)
            {
                this.extentsHorizontal = 1;
            }

            if (this.extentsHorizontal * this.extentsVertical > this.extentCount)
            {
                this.VisibleExtents = this.extentCount;
                this.scrollBar.Enabled = false;
            }
            else
            {
                this.scrollBar.Enabled = true;
                this.VisibleExtents = this.extentsHorizontal * this.extentsVertical;
            }

            this.scrollBar.Maximum = this.extentCount + this.extentsHorizontal;

            if (this.extentsHorizontal > this.extentCount)
            {
                this.extentsHorizontal = this.extentCount;
            }

            if (this.extentsVertical > (this.extentCount / this.extentsHorizontal))
            {
                this.extentsVertical = (this.extentCount / this.extentsHorizontal);
            }

            this.extentsRemaining = this.extentCount - (this.extentsHorizontal * this.extentsVertical);
        }

        /// <summary>
        /// Draws the selected range.
        /// </summary>
        /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
        private void DrawSelectedRange(PaintEventArgs e)
        {
            if (this.selectionStartExtent > 0)
            {
                for (int extent = this.selectionStartExtent; extent < (this.selectionEndExtent < 0 ? this.provisionalEndExtent : this.selectionEndExtent); extent++)
                {
                    this.pageExtentRenderer.DrawSelection(e.Graphics, this.ExtentPosition(extent));
                }
            }
        }

        /// <summary>
        /// Handles the DoWork event of the ImageBufferBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void ImageBufferBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            this.Invalidate();

            e.Result = FullMapRenderer.RenderMapLayers((BackgroundWorker)sender, this.MapLayers, this.Bounds, this.FileId, this.File.Size);
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the ImageBufferBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void ImageBufferBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.BackgroundImage = (Bitmap)e.Result;
            this.BackgroundImageLayout = ImageLayout.Stretch;

            this.HoldingMessage = string.Empty;

            this.Refresh();
        }

        /// <summary>
        /// Handles the ProgressChanged event of the ImageBufferBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void ImageBufferBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.BackgroundImage = (Bitmap)e.UserState;
            this.BackgroundImageLayout = ImageLayout.Stretch;

            this.Refresh();
        }

        #region Events

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.Paint"/> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"/> that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.imageBufferBackgroundWorker.IsBusy || this.Holding)
            {
                // SolidBrush brush = new SolidBrush(Color.FromArgb(200, Color.Black));
                e.Graphics.FillRectangle(backgroundBrush, this.Bounds);

                //for (int l = 0; l < Height; l += 4)
                //{
                //    e.Graphics.DrawLine(this.backgroundLine, 0, l, Width, l);
                //}

                TextRenderer.DrawText(e.Graphics, this.HoldingMessage, this.Font, new Point(4, 4), Color.Black);
            }
            else
            {
                if (!e.ClipRectangle.IsEmpty && this.extentCount > 0 && Visible)
                {
                    if (this.Mode != MapMode.Full)
                    {
                        // e.Graphics.FillRectangle(backgroundBrush, this.Bounds);

                        //for (int l = 0; l < Height; l += 4)
                        //{
                        //    e.Graphics.DrawLine(this.backgroundLine, 0, l, Width, l);
                        //}

                        this.CalculateVisibleExtents();

                        this.pageExtentRenderer.DrawBackgroundExtents(e,
                                                                     this.ExtentSize,
                                                                     this.extentsHorizontal,
                                                                     this.extentsVertical,
                                                                     this.extentsRemaining);
                    }
                    else
                    {
                        this.scrollBar.Enabled = false;
                    }

                    switch (this.mode)
                    {
                        case MapMode.Standard:

                            if (this.extentCount > 0)
                            {
                                this.DrawExtents(e);
                            }

                            this.DrawSinglePages(e);

                            int mapWidth = this.extentsHorizontal * this.extentSize.Width;

                            e.Graphics.FillRectangle(backgroundBrush, mapWidth, 0, this.Width - mapWidth, this.Height);

                            e.Graphics.DrawLine(SystemPens.ControlDark, mapWidth, 0, mapWidth, this.Height);

                            break;

                        case MapMode.Pfs:

                            DrawPfsPages(e);

                            break;

                        case MapMode.Map:

                            // DrawMapPage(e);
                            break;

                        case MapMode.RangeSelection:

                            this.DrawSelectedRange(e);
                            break;

                        case MapMode.Full:

                            base.OnPaint(e);
                            break;
                    }
                }
            }



            ControlPaint.DrawBorder(e.Graphics,
                                    new Rectangle(0, 0, Width, Height),
                                    SystemColors.ControlDark,
                                    ButtonBorderStyle.Solid);
        }

        /// <summary>
        /// Called when [window position changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        internal virtual void OnWindowPositionChanged(object sender, EventArgs e)
        {
            if (this.WindowPositionChanged != null)
            {
                this.WindowPositionChanged(sender, e);
            }
        }

        /// <summary>
        /// Handles the MouseClick event of the AllocationMapPanel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void AllocationMapPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int newSelectedBlock = this.ExtentPosition(e.X, e.Y);

                if (newSelectedBlock != this.SelectedPage)
                {
                    int page = this.PagePosition(e.X, e.Y) + (this.WindowPosition * 8);

                    if (page <= (this.extentCount * 8))
                    {
                        this.SelectedPage = this.PagePosition(e.X, e.Y) + (this.WindowPosition * 8);

                        if (this.Mode == MapMode.RangeSelection)
                        {
                            if (this.selectionStartExtent <= 0)
                            {
                                this.selectionStartExtent = newSelectedBlock;
                            }
                            else
                            {
                                this.selectionEndExtent = newSelectedBlock;

                                EventHandler temp = this.RangeSelected;
                                if (temp != null)
                                {
                                    temp(this, EventArgs.Empty);
                                }
                            }
                        }
                        else
                        {
                            EventHandler<PageEventArgs> temp = this.PageClicked;

                            if (temp != null)
                            {
                                bool openInNewWindow = Control.ModifierKeys == Keys.Shift;

                                temp(this, new PageEventArgs(new RowIdentifier(this.FileId, page + this.startPage.PageId, 0), openInNewWindow));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the MouseMove event of the AllocationMapPanel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void AllocationMapPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.Mode != MapMode.Full)
            {
                int newSelectedBlock = this.ExtentPosition(e.X, e.Y);

                if (newSelectedBlock != this.SelectedPage)
                {
                    int page = this.PagePosition(e.X, e.Y) + (this.WindowPosition * 8);

                    if (page <= (this.extentCount * 8))
                    {
                        EventHandler<PageEventArgs> temp = this.PageOver;

                        if (temp != null)
                        {
                            temp(this, new PageEventArgs(new RowIdentifier(this.startPage.FileId, page + this.startPage.PageId, 0), false));
                        }

                        if (this.Mode == MapMode.RangeSelection)
                        {
                            if (this.provisionalEndExtent != newSelectedBlock)
                            {
                                this.provisionalEndExtent = newSelectedBlock;
                                this.Invalidate();
                            }
                        }
                    }
                }
            }
        }

        private void DrawPfsPages(PaintEventArgs e)
        {
            if (pfs != null)
            {
                for (int i = 0; i < (visibleExtents * 8) && i + (WindowPosition * 8) < extentCount * 8; i++)
                {
                    int pageId = i + (WindowPosition * 8);

                    pfsRenderer.DrawPfsPage(e.Graphics, PagePosition(i), Pfs.PagePfsByte(pageId));
                }
            }
        }

        /// <summary>
        /// Handles the ValueChanged event of the ScrollBar control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void ScrollBar_ValueChanged(object sender, EventArgs e)
        {
            this.WindowPosition = this.scrollBar.Value - (this.scrollBar.Value % this.extentsHorizontal);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the file id.
        /// </summary>
        /// <value>The file id.</value>
        public int FileId
        {
            get { return this.fileId; }
            set { this.fileId = value; }
        }

        /// <summary>
        /// Gets or sets the map layers.
        /// </summary>
        /// <value>The map layers.</value>
        public List<AllocationLayer> MapLayers
        {
            get { return this.mapLayers; }
            set { this.mapLayers = value; }
        }

        /// <summary>
        /// Gets or sets the number of visible extents.
        /// </summary>
        /// <value>The number visible extents.</value>
        public int VisibleExtents
        {
            get { return this.visibleExtents; }
            set { this.visibleExtents = value; }
        }

        /// <summary>
        /// Gets or sets the border colour.
        /// </summary>
        /// <value>The border colour.</value>
        public Color BorderColour
        {
            get { return this.pageExtentRenderer.PageBorderColour; }
            set { this.pageExtentRenderer.PageBorderColour = value; }
        }

        /// <summary>
        /// Gets or sets the selected page.
        /// </summary>
        /// <value>The selected page.</value>
        public int SelectedPage
        {
            get { return this.selectedPage + this.startPage.PageId; }
            set { this.selectedPage = value; }
        }

        /// <summary>
        /// Gets or sets the size of the extent.
        /// </summary>
        /// <value>The size of the extent.</value>
        public Size ExtentSize
        {
            get
            {
                return this.extentSize;
            }

            set
            {
                this.extentSize = value;
                this.CalculateVisibleExtents();

                this.pageExtentRenderer.ResizeExtentBrush(this.extentSize);
            }
        }

        /// <summary>
        /// Gets or sets the extent count.
        /// </summary>
        /// <value>The extent count.</value>
        public int ExtentCount
        {
            get { return this.extentCount; }
            set { this.extentCount = value; }
        }

        /// <summary>
        /// Gets or sets the window position.
        /// </summary>
        /// <value>The window position.</value>
        public int WindowPosition
        {
            get
            {
                return this.windowPosition;
            }

            set
            {
                this.windowPosition = value;
                this.scrollBar.Value = this.windowPosition;
                this.OnWindowPositionChanged(this, EventArgs.Empty);
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether IAMs are included.
        /// </summary>
        /// <value><c>true</c> if [include iam]; otherwise, <c>false</c>.</value>
        public bool IncludeIam
        {
            get { return this.includeIam; }
            set { this.includeIam = value; }
        }

        /// <summary>
        /// Gets or sets the allocation map mode.
        /// </summary>
        /// <value>The allocation map mode.</value>
        public MapMode Mode
        {
            get
            {
                return this.mode;
            }

            set
            {
                this.mode = value;
                this.BackgroundImage = null;
            }
        }

        /// <summary>
        /// Gets or sets the selection start extent.
        /// </summary>
        /// <value>The selection start extent.</value>
        public int SelectionStartExtent
        {
            get { return this.selectionStartExtent; }
            set { this.selectionStartExtent = value; }
        }

        /// <summary>
        /// Gets or sets the selection end extent.
        /// </summary>
        /// <value>The selection end extent.</value>
        public int SelectionEndExtent
        {
            get { return this.selectionEndExtent; }
            set { this.selectionEndExtent = value; }
        }

        /// <summary>
        /// Gets or sets the start page.
        /// </summary>
        /// <value>The start page.</value>
        public PageAddress StartPage
        {
            get
            {
                return this.startPage;
            }

            set
            {
                this.startPage = value;
            }
        }

        /// <summary>
        /// Gets or sets the file.
        /// </summary>
        /// <value>The file.</value>
        public DatabaseFile File
        {
            get { return this.file; }
            set { this.file = value; }
        }

        /// <summary>
        /// Gets or sets if a border is drawn round the map
        /// </summary>
        public bool DrawBorder
        {
            get { return this.pageExtentRenderer.DrawBorder; }
            set { this.pageExtentRenderer.DrawBorder = value; }
        }

        /// <summary>
        /// Gets or sets if the map is in a holding state
        /// </summary>
        public bool Holding
        {
            get { return this.holding; }
            set { this.holding = value; }
        }

        /// <summary>
        /// Gets or sets the holding status message
        /// </summary>
        public string HoldingMessage
        {
            get { return this.holdingMessage; }
            set { this.holdingMessage = value; }
        }

        public Pfs Pfs
        {
            get { return pfs; }
            set { pfs = value; }
        }

        #endregion

        void IDisposable.Dispose()
        {
            this.backgroundLine.Dispose();
            this.pageExtentRenderer.Dispose();
            this.backgroundBrush.Dispose();
        }
    }
}
