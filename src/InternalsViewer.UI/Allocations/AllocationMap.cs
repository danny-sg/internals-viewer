using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.Renderers;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Allocations;

/// <summary>
/// Allocation Map used to display allocation layers for files
/// </summary>
public class AllocationMap : Panel, IDisposable
{
    public static Size Large = new(256, 32);
    public static Size Medium = new(96, 12);
    public static Size Small = new(64, 8);

    private readonly Color defaultPageBorderColour = Color.White;
    private int extentsHorizontal;
    private int extentsRemaining;
    private int extentsVertical;
    private int windowPosition;
    private Size extentSize;
    private MapMode mode;
    private readonly VScrollBar scrollBar;
    private int selectedPage = -1;

    public event EventHandler<PageEventArgs>? PageClicked;
    public event EventHandler? RangeSelected;
    public event EventHandler<PageEventArgs>? PageOver;
    public event EventHandler? WindowPositionChanged;

    private readonly PageExtentRenderer pageExtentRenderer;
    private readonly PfsRenderer pfsRenderer;

    private int provisionalEndExtent;
    private readonly BackgroundWorker imageBufferBackgroundWorker = new();

    private readonly Pen backgroundLine = new(Color.FromArgb(242, 242, 242), 2);
    private readonly LinearGradientBrush backgroundBrush;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationMap"/> class.
    /// </summary>
    public AllocationMap()
    {
        SuspendLayout();

        BackColor = Color.White;

        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.DoubleBuffer, true);

        Padding = new Padding(1);
        scrollBar = new VScrollBar();
        scrollBar.Enabled = false;
        scrollBar.Dock = DockStyle.Right;

        Controls.Add(scrollBar);

        ResumeLayout(false);

        MouseClick += AllocationMapPanel_MouseClick;
        scrollBar.ValueChanged += ScrollBar_ValueChanged;
        MouseMove += AllocationMapPanel_MouseMove;
        Resize += AllocationMap_Resize;
        extentSize = Small;

        imageBufferBackgroundWorker.DoWork += ImageBufferBackgroundWorker_DoWork;
        imageBufferBackgroundWorker.RunWorkerCompleted += ImageBufferBackgroundWorker_RunWorkerCompleted;
        imageBufferBackgroundWorker.ProgressChanged += ImageBufferBackgroundWorker_ProgressChanged;

        imageBufferBackgroundWorker.WorkerReportsProgress = true;
        imageBufferBackgroundWorker.WorkerSupportsCancellation = true;

        pageExtentRenderer = new PageExtentRenderer(Color.WhiteSmoke, Color.FromArgb(234, 234, 234));
        pfsRenderer = new PfsRenderer(new Rectangle(0, 0, Large.Height, Large.Width / 8), Color.WhiteSmoke);

        pageExtentRenderer.CreateBrushesAndPens(ExtentSize);

        backgroundBrush = new LinearGradientBrush(ClientRectangle,
                                                  SystemColors.Control,
                                                  SystemColors.ControlLightLight,
                                                  LinearGradientMode.Horizontal);

        backgroundBrush.WrapMode = WrapMode.TileFlipX;

        MapLayers = new List<AllocationLayer>();
    }

    private void AllocationMap_Resize(object? sender, EventArgs e)
    {
        backgroundBrush.ResetTransform();

        backgroundBrush.ScaleTransform(Bounds.Height / backgroundBrush.Rectangle.Height,
                                       Bounds.Width / backgroundBrush.Rectangle.Width,
                                       MatrixOrder.Append);
    }

    /// <summary>
    /// Begins the async full map render
    /// </summary>
    public void ShowFullMap()
    {
        HoldingMessage = "Rendering...";

        if (imageBufferBackgroundWorker.IsBusy)
        {
            imageBufferBackgroundWorker.CancelAsync();
        }

        while (imageBufferBackgroundWorker.IsBusy)
        {
            Application.DoEvents();
        }

        imageBufferBackgroundWorker.RunWorkerAsync();
        BackgroundImageLayout = ImageLayout.Stretch;
    }

    /// <summary>
    /// Draws the pages in the single page slots for the allocations the map displays
    /// </summary>
    private void DrawSinglePages(PaintEventArgs e)
    {
        pageExtentRenderer.ResizePageBrush(ExtentSize);

        foreach (var layer in MapLayers)
        {
            if (layer.IsVisible)
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

                pageExtentRenderer.SetExtentBrushColour(pageColour, ExtentColour.LightBackgroundColour(pageColour));

                if (layer.UseBorderColour)
                {
                    pageExtentRenderer.PageBorderColour = layer.BorderColour;
                }
                else
                {
                    pageExtentRenderer.PageBorderColour = defaultPageBorderColour;
                }

                foreach (var allocation in layer.Allocations)
                {
                    foreach (var address in allocation.SinglePageSlots)
                    {
                        if (address.FileId == FileId && address.PageId != 0 && CheckPageVisible(address.PageId))
                        {
                            pageExtentRenderer.DrawPage(e.Graphics,
                                PagePosition(address.PageId - (WindowPosition * 8)),
                                layer.LayerType);
                        }
                    }

                    if (IncludeIam)
                    {
                        foreach (var page in allocation.Pages)
                        {
                            if (CheckPageVisible(page.PageAddress.PageId))
                            {
                                pageExtentRenderer.DrawPage(e.Graphics,
                                                            PagePosition(page.PageAddress.PageId - (WindowPosition * 8)),
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
    private void DrawExtents(PaintEventArgs e)
    {
        pageExtentRenderer.ResizeExtentBrush(ExtentSize);

        for (var extent = windowPosition;
             extent < ExtentCount && extent < (VisibleExtents + windowPosition);
             extent++)
        {
            foreach (var layer in MapLayers)
            {
                if (layer.IsVisible && !layer.SingleSlotsOnly)
                {
                    foreach (var chain in layer.Allocations)
                    {
                        var targetExtent = extent + (StartPage.PageId / 8);

                        if (AllocationChain.GetAllocatedStatus(targetExtent, FileId, layer.IsInverted, chain))
                        {
                            pageExtentRenderer.SetExtentBrushColour(layer.Colour,
                                                                 ExtentColour.BackgroundColour(layer.Colour));

                            pageExtentRenderer.DrawExtent(e.Graphics, ExtentPosition(extent - WindowPosition));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks the page is visible on the map
    /// </summary>
    private bool CheckPageVisible(int pageId)
    {
        return pageId >= (windowPosition * 8) && pageId <= ((VisibleExtents + windowPosition) * 8);
    }

    /// <summary>
    /// Get Rectangle for a particular extent
    /// </summary>
    private Rectangle ExtentPosition(int extent)
    {
        if (extentsHorizontal > 1)
        {
            return new Rectangle((extent * extentSize.Width) % (extentsHorizontal * extentSize.Width),
                (int)Math.Floor((decimal)extent / extentsHorizontal) * extentSize.Height,
                extentSize.Width,
                extentSize.Height);
        }

        return new Rectangle(0, 0, extentSize.Width, extentSize.Height);
    }

    /// <summary>
    /// Get the position for a particular page
    /// </summary>
    private Rectangle PagePosition(int page)
    {
        var pageWidth = extentSize.Width / 8;

        if (page != 0)
        {
            return new Rectangle((page * pageWidth) % ((extentsHorizontal * 8) * pageWidth),
                (int)Math.Floor((decimal)page / (extentsHorizontal * 8)) * extentSize.Height,
                pageWidth,
                extentSize.Height);
        }

        return new Rectangle(0, 0, pageWidth, extentSize.Height);
    }

    /// <summary>
    /// Get the extent at a particular x and y position
    /// </summary>
    private int ExtentPosition(int x, int y)
    {
        return 1 + (y / extentSize.Height * extentsHorizontal) + (x / extentSize.Width);
    }

    /// <summary>
    /// Get the page at a particular x and y position
    /// </summary>
    private int PagePosition(int x, int y)
    {
        return (y / extentSize.Height * (extentsHorizontal * 8)) + (x / (extentSize.Width / 8));
    }

    /// <summary>
    /// Calculates the number (horizontal and vertical) of the visible extents.
    /// </summary>
    internal void CalculateVisibleExtents()
    {
        extentsHorizontal = (int)Math.Floor((decimal)(Width - scrollBar.Width) / extentSize.Width);
        extentsVertical = (int)Math.Ceiling((decimal)Height / extentSize.Height);

        if (extentsHorizontal == 0 | extentsVertical == 0 | ExtentCount == 0)
        {
            return;
        }

        extentsRemaining = ExtentCount - (extentsHorizontal * extentsVertical);

        scrollBar.SmallChange = extentsHorizontal;
        scrollBar.LargeChange = (extentsVertical - 1) * extentsHorizontal;

        if (extentsHorizontal == 0)
        {
            extentsHorizontal = 1;
        }

        if (extentsHorizontal * extentsVertical > ExtentCount)
        {
            VisibleExtents = ExtentCount;
            scrollBar.Enabled = false;
        }
        else
        {
            scrollBar.Enabled = true;
            VisibleExtents = extentsHorizontal * extentsVertical;
        }

        scrollBar.Maximum = ExtentCount + extentsHorizontal;

        if (extentsHorizontal > ExtentCount)
        {
            extentsHorizontal = ExtentCount;
        }

        if (extentsVertical > (ExtentCount / extentsHorizontal))
        {
            extentsVertical = (ExtentCount / extentsHorizontal);
        }

        extentsRemaining = ExtentCount - (extentsHorizontal * extentsVertical);
    }

    /// <summary>
    /// Draws the selected range.
    /// </summary>
    private void DrawSelectedRange(PaintEventArgs e)
    {
        if (SelectionStartExtent > 0)
        {
            for (var extent = SelectionStartExtent;
                 extent < (SelectionEndExtent < 0 ? provisionalEndExtent : SelectionEndExtent);
                 extent++)
            {
                pageExtentRenderer.DrawSelection(e.Graphics, ExtentPosition(extent));
            }
        }
    }

    /// <summary>
    /// Handles the DoWork event of the ImageBufferBackgroundWorker control.
    /// </summary>
    private void ImageBufferBackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
    {
        Invalidate();

        e.Result = FullMapRenderer.RenderMapLayers(MapLayers, Bounds, FileId, File.Size);
    }

    /// <summary>
    /// Handles the RunWorkerCompleted event of the ImageBufferBackgroundWorker control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
    private void ImageBufferBackgroundWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        BackgroundImage = (Bitmap)e.Result!;
        BackgroundImageLayout = ImageLayout.Stretch;

        HoldingMessage = string.Empty;

        Refresh();
    }

    /// <summary>
    /// Handles the ProgressChanged event of the ImageBufferBackgroundWorker control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
    private void ImageBufferBackgroundWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        BackgroundImage = (Bitmap)e.UserState!;
        BackgroundImageLayout = ImageLayout.Stretch;

        Refresh();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (imageBufferBackgroundWorker.IsBusy || Holding)
        {
            // SolidBrush brush = new SolidBrush(Color.FromArgb(200, Color.Black));
            e.Graphics.FillRectangle(backgroundBrush, Bounds);

            //for (int l = 0; l < Height; l += 4)
            //{
            //    e.Graphics.DrawLine(this.backgroundLine, 0, l, Width, l);
            //}

            TextRenderer.DrawText(e.Graphics, HoldingMessage, Font, new Point(4, 4), Color.Black);
        }
        else
        {
            if (!e.ClipRectangle.IsEmpty && ExtentCount > 0 && Visible)
            {
                if (Mode != MapMode.Full)
                {
                    // e.Graphics.FillRectangle(backgroundBrush, this.Bounds);

                    //for (int l = 0; l < Height; l += 4)
                    //{
                    //    e.Graphics.DrawLine(this.backgroundLine, 0, l, Width, l);
                    //}

                    CalculateVisibleExtents();

                    pageExtentRenderer.DrawBackgroundExtents(e,
                        ExtentSize,
                        extentsHorizontal,
                        extentsVertical,
                        extentsRemaining);
                }
                else
                {
                    scrollBar.Enabled = false;
                }

                switch (mode)
                {
                    case MapMode.Standard:

                        if (ExtentCount > 0)
                        {
                            DrawExtents(e);
                        }

                        DrawSinglePages(e);

                        var mapWidth = extentsHorizontal * extentSize.Width;

                        e.Graphics.FillRectangle(backgroundBrush, mapWidth, 0, Width - mapWidth, Height);

                        e.Graphics.DrawLine(SystemPens.ControlDark, mapWidth, 0, mapWidth, Height);

                        break;

                    case MapMode.Pfs:

                        DrawPfsPages(e);

                        break;

                    case MapMode.Map:

                        // DrawMapPage(e);
                        break;

                    case MapMode.RangeSelection:

                        DrawSelectedRange(e);
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

    internal virtual void OnWindowPositionChanged(object? sender, EventArgs e)
    {
        WindowPositionChanged?.Invoke(sender, e);
    }

    /// <summary>
    /// Handles the MouseClick event of the AllocationMapPanel control.
    /// </summary>
    private void AllocationMapPanel_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var newSelectedBlock = ExtentPosition(e.X, e.Y);

            if (newSelectedBlock != SelectedPage)
            {
                var page = PagePosition(e.X, e.Y) + (WindowPosition * 8);

                if (page <= (ExtentCount * 8))
                {
                    SelectedPage = PagePosition(e.X, e.Y) + (WindowPosition * 8);

                    if (Mode == MapMode.RangeSelection)
                    {
                        if (SelectionStartExtent <= 0)
                        {
                            SelectionStartExtent = newSelectedBlock;
                        }
                        else
                        {
                            SelectionEndExtent = newSelectedBlock;

                            var temp = RangeSelected;
                            if (temp != null)
                            {
                                temp(this, EventArgs.Empty);
                            }
                        }
                    }
                    else
                    {
                        var temp = PageClicked;

                        if (temp != null)
                        {
                            var openInNewWindow = ModifierKeys == Keys.Shift;

                            temp(this, new PageEventArgs(new PageAddress(FileId, page + StartPage.PageId), openInNewWindow));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles the MouseMove event of the AllocationMapPanel control.
    /// </summary>
    private void AllocationMapPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (Mode != MapMode.Full)
        {
            var newSelectedBlock = ExtentPosition(e.X, e.Y);

            if (newSelectedBlock != SelectedPage)
            {
                var page = PagePosition(e.X, e.Y) + (WindowPosition * 8);

                if (page <= (ExtentCount * 8))
                {
                    PageOver?.Invoke(this, new PageEventArgs(new PageAddress(FileId, page + StartPage.PageId), false));

                    if (Mode == MapMode.RangeSelection)
                    {
                        if (provisionalEndExtent != newSelectedBlock)
                        {
                            provisionalEndExtent = newSelectedBlock;

                            Invalidate();
                        }
                    }
                }
            }
        }
    }

    private void DrawPfsPages(PaintEventArgs e)
    {
        if (Pfs != null)
        {
            for (var i = 0; i < (VisibleExtents * 8) && i + (WindowPosition * 8) < ExtentCount * 8; i++)
            {
                var pageId = i + (WindowPosition * 8);

                pfsRenderer.DrawPfsPage(e.Graphics, PagePosition(i), Pfs.GetPagePfsStatus(pageId));
            }
        }
    }

    private void ScrollBar_ValueChanged(object? sender, EventArgs e)
    {
        WindowPosition = scrollBar.Value - (scrollBar.Value % extentsHorizontal);
    }

    public short FileId { get; set; }

    public List<AllocationLayer> MapLayers { get; set; }

    public int VisibleExtents { get; set; }

    public Color BorderColour
    {
        get => pageExtentRenderer.PageBorderColour;
        set => pageExtentRenderer.PageBorderColour = value;
    }

    public int SelectedPage
    {
        get => selectedPage + StartPage.PageId;
        set => selectedPage = value;
    }

    public Size ExtentSize
    {
        get => extentSize;

        set
        {
            extentSize = value;
            CalculateVisibleExtents();

            pageExtentRenderer.ResizeExtentBrush(extentSize);
        }
    }

    public int ExtentCount { get; set; }

    public int WindowPosition
    {
        get => windowPosition;

        set
        {
            windowPosition = value;
            scrollBar.Value = windowPosition;
            OnWindowPositionChanged(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether IAMs are included.
    /// </summary>
    public bool IncludeIam { get; set; }

    public MapMode Mode
    {
        get => mode;

        set
        {
            mode = value;
            BackgroundImage = null;
        }
    }

    public int SelectionStartExtent { get; set; } = -1;

    public int SelectionEndExtent { get; set; } = -1;

    public PageAddress StartPage { get; set; } = new(1, 0);

    public DatabaseFile File { get; set; }

    public bool DrawBorder
    {
        get => pageExtentRenderer.DrawBorder;
        set => pageExtentRenderer.DrawBorder = value;
    }

    public bool Holding { get; set; }

    public string HoldingMessage { get; set; }

    public PfsChain? Pfs { get; set; }

    public new void Dispose()
    {
        base.Dispose();

        backgroundLine.Dispose();
        pageExtentRenderer.Dispose();
        backgroundBrush.Dispose();
    }
}