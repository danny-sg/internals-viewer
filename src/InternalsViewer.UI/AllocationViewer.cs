using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Allocations;
using InternalsViewer.UI.Markers;

#pragma warning disable CA1416

namespace InternalsViewer.UI;

/// <summary>
/// Allocation Viewer control for Allocation Pages (IAM, GAM, SGAM, PFS etc.)
/// </summary>
public partial class AllocationViewer : UserControl
{
    public event EventHandler<PageEventArgs> PageOver;
    public event EventHandler<PageEventArgs> PageClicked;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationViewer"/> class.
    /// </summary>
    public AllocationViewer()
    {
        InitializeComponent();

        allocationMap.PageOver += OnPageOver;
        allocationMap.PageClicked += OnPageClicked;

        SetColours(CreateIamColours());
    }

    /// <summary>
    /// Creates the IAM key colours for the header information
    /// </summary>
    /// <returns></returns>
    private static List<Color> CreateIamColours()
    {
        var colours = new List<Color>(9);

        colours.Add(Color.LightGreen);
        colours.Add(Color.Lavender);
        colours.Add(Color.LightBlue);
        colours.Add(Color.LightSteelBlue);
        colours.Add(Color.LightSkyBlue);
        colours.Add(Color.Azure);
        colours.Add(Color.PowderBlue);
        colours.Add(Color.SkyBlue);
        colours.Add(Color.AliceBlue);

        return colours;
    }

    /// <summary>
    /// Sets the IAM header information.
    /// </summary>
    /// <param name="page">The page.</param>
    private void SetIamInformation(AllocationPage page)
    {
        if (page.SinglePageSlots.Count == 8)
        {
            slotZeroTextBox.Text = page.SinglePageSlots[0].ToString() ?? string.Empty;
            slotOneTextBox.Text = page.SinglePageSlots[1].ToString() ?? string.Empty;
            slotTwoTextBox.Text = page.SinglePageSlots[2].ToString() ?? string.Empty;
            slotThreeTextBox.Text = page.SinglePageSlots[3].ToString() ?? string.Empty;
            slotFourTextBox.Text = page.SinglePageSlots[4].ToString() ?? string.Empty;
            slotFiveTextBox.Text = page.SinglePageSlots[5].ToString() ?? string.Empty;
            slotSixTextBox.Text = page.SinglePageSlots[6].ToString() ?? string.Empty;
            slotSevenTextBox.Text = page.SinglePageSlots[7].ToString() ?? string.Empty;
            startPageTextBox.Text = page.StartPage.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Sets the header key images
    /// </summary>
    /// <param name="colours">The colours.</param>
    public void SetColours(List<Color> colours)
    {
        startPageBox.Image = ExtentColour.KeyImage(colours[0]);
        slot0Box.Image = ExtentColour.KeyImage(colours[1]);
        slot1Box.Image = ExtentColour.KeyImage(colours[2]);
        slot2Box.Image = ExtentColour.KeyImage(colours[3]);
        slot3Box.Image = ExtentColour.KeyImage(colours[4]);
        slot4Box.Image = ExtentColour.KeyImage(colours[5]);
        slot5Box.Image = ExtentColour.KeyImage(colours[6]);
        slot6Box.Image = ExtentColour.KeyImage(colours[7]);
        slot7Box.Image = ExtentColour.KeyImage(colours[8]);
    }

    /// <summary>
    /// Sets an allocation page to be displayed
    /// </summary>
    /// <param name="pageAddress">The page address.</param>
    /// <param name="databaseName">Name of the database.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="showHeader">if set to <c>true</c> [show header].</param>
    /// <returns></returns>
    public List<Marker> SetAllocationPage(PageAddress pageAddress, string databaseName, string connectionString, bool showHeader)
    {
        topPanel.Visible = showHeader;

        var allocationPage = new AllocationPage();

        allocationMap.Mode = MapMode.Standard;
        allocationMap.ExtentCount = 63903;
        allocationMap.ExtentSize = AllocationMap.Small;

        allocationMap.StartPage = allocationPage.StartPage;
        allocationMap.FileId = allocationPage.StartPage.FileId;

        var layer = new AllocationLayer(allocationPage.PageAddress.ToString(),
            allocationPage,
            Color.Brown);

        layer.SingleSlotsOnly = false;

        allocationMap.MapLayers.Clear();
        allocationMap.MapLayers.Add(layer);

        allocationMap.Invalidate();

        if (showHeader)
        {
            SetIamInformation(allocationPage);
        }

        var markers = MarkerBuilder.BuildMarkers(allocationPage, string.Empty);

        return markers;
    }

    /// <summary>
    /// Sets the PFS page to be displayed
    /// </summary>
    /// <param name="pageAddress">The page address.</param>
    /// <param name="databaseName">Name of the database.</param>
    /// <param name="connectionString">The connection string.</param>
    public void SetPfsPage(PageAddress pageAddress, string databaseName, string connectionString)
    {
        topPanel.Visible = false;

        var pfsPage = new PfsPage();

        allocationMap.Mode = MapMode.Pfs;

        allocationMap.Pfs = new PfsChain();
        allocationMap.ExtentCount = 1011;
        allocationMap.ExtentSize = AllocationMap.Large;

        if (pfsPage.PageAddress.PageId > 1)
        {
            allocationMap.StartPage = pfsPage.PageAddress;
        }
        else
        {
            allocationMap.StartPage = new PageAddress(pfsPage.PageAddress.FileId, 0);
        }

        allocationMap.Invalidate();
    }

    /// <summary>
    /// Called when the mouse hovers over a page on the allocation map
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="PageEventArgs"/> instance containing the event data.</param>
    internal virtual void OnPageOver(object sender, PageEventArgs e)
    {
        if (PageOver != null)
        {
            PageOver(sender, e);
        }
    }

    /// <summary>
    /// Called when a page is clicked on the allocation map
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="PageEventArgs"/> instance containing the event data.</param>
    internal virtual void OnPageClicked(object sender, PageEventArgs e)
    {
        PageClicked?.Invoke(sender, e);
    }

    /// <summary>
    /// Handles the Click event of various text boxes with page addresses
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void PageAddressTextBox_Click(object sender, EventArgs e)
    {
        var pageAddress = PageAddressParser.Parse((sender as TextBox).Text);

        OnPageClicked(sender, new PageEventArgs(new RowIdentifier(pageAddress, 0), false));
    }

    /// <summary>
    /// Handles the Paint event of the TopPanel control to draw a border around it
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
    private void TopPanel_Paint(object sender, PaintEventArgs e)
    {
        ControlPaint.DrawBorder(e.Graphics,
            new Rectangle(topPanel.Bounds.X, topPanel.Bounds.Y, topPanel.Width, topPanel.Height + 2),
            SystemColors.ControlDark,
            ButtonBorderStyle.Solid);
    }
}