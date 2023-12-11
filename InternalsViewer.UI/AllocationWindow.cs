using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Allocations;
#pragma warning disable CA1416

namespace InternalsViewer.UI;

public partial class AllocationWindow : UserControl
{
    public event EventHandler Connect;
    public event EventHandler<PageEventArgs> ViewPage;

    protected delegate void LoadDatabaseDelegate();
    private readonly BufferPool bufferPool = new();
    private bool keyChanging;
    private const string AllocationMapText = "Allocation Map";
    private const string AllocationUnitsText = "Allocation Units";
    private const string PageFreeSpaceText = "PFS";

    public AllocationWindow()
    {
        InitializeComponent();

        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.DoubleBuffer, true);

        extentSizeToolStripComboBox.SelectedIndex = 0;
    }
        
    /// <summary>
    /// Enables buttons on the toolbar.
    /// </summary>
    /// <param name="enabled">if set to <c>true</c> [enabled].</param>
    private void EnableToolbar(bool enabled)
    {
        databaseToolStripComboBox.Enabled = enabled;
        extentSizeToolStripComboBox.Enabled = enabled;
        bufferPoolToolStripButton.Enabled = enabled;
        fileDetailsToolStripButton.Enabled = enabled;
        showKeyToolStripButton.Enabled = enabled;
        mapToolStripButton.Enabled = enabled;
    }

    /// <summary>
    /// Refreshes the databases list
    /// </summary>
    public void RefreshDatabases()
    {
        databaseToolStripComboBox.ComboBox.DataSource = null;

        databaseToolStripComboBox.ComboBox.Items.Clear();

        EnableToolbar(InternalsViewerConnection.CurrentConnection().Databases.Count > 0);

        databaseToolStripComboBox.ComboBox.DataSource = InternalsViewerConnection.CurrentConnection().Databases;
        databaseToolStripComboBox.ComboBox.DisplayMember = "Name";
        databaseToolStripComboBox.ComboBox.ValueMember = "DatabaseId";
    }

    /// <summary>
    /// Loads the selected database.
    /// </summary>
    private void LoadDatabase()
    {
        if (allocationContainer.InvokeRequired)
        {
            Invoke(new LoadDatabaseDelegate(LoadDatabase));
        }
        else
        {
            if (databaseToolStripComboBox.ComboBox.SelectedItem != InternalsViewerConnection.CurrentConnection().CurrentDatabase)
            {
                databaseToolStripComboBox.ComboBox.SelectedItem = InternalsViewerConnection.CurrentConnection().CurrentDatabase;
            }

            if (InternalsViewerConnection.CurrentConnection().CurrentDatabase != null)
            {
                allocationContainer.CreateAllocationMaps(InternalsViewerConnection.CurrentConnection().CurrentDatabase.Files);
            }

            CancelWorkerAndWait(allocUnitBackgroundWorker);

            if (mapToolStripButton.Text == AllocationMapText)
            {
                DisplayAllocationMapLayers();
            }
            else
            {
                DisplayAllocationUnitLayers();
            }
        }
    }

    /// <summary>
    /// Displays the allocation layers.
    /// </summary>
    private void DisplayAllocationUnitLayers()
    {
        allocationContainer.IncludeIam = false;
        allocationContainer.ClearMapLayers();

        var unallocated = new AllocationLayer();

        unallocated.Name = "Available - (Unused)";
        unallocated.Colour = Color.Gainsboro;
        unallocated.Visible = false;

        allocationContainer.AddMapLayer(unallocated);

        CancelWorkerAndWait(allocUnitBackgroundWorker);

        allocUnitBackgroundWorker.RunWorkerAsync();
    }

    /// <summary>
    /// Cancels a BackgroundWorker and wait for it to complete
    /// </summary>
    /// <param name="worker">The worker.</param>
    private void CancelWorkerAndWait(BackgroundWorker worker)
    {
        if (worker.IsBusy)
        {
            worker.CancelAsync();
        }

        while (worker.IsBusy)
        {
            Application.DoEvents();
        }
    }

    /// <summary>
    /// Handles the DoWork event of the AllocUnitBackgroundWorker control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
    private void AllocUnitBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
    {
        e.Result = AllocationUnitsLayer.GenerateLayers(InternalsViewerConnection.CurrentConnection().CurrentDatabase, (BackgroundWorker)sender, true, false);
    }

    /// <summary>
    /// Handles the ProgressChanged event of the AllocUnitBackgroundWorker control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
    private void AllocUnitBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        allocUnitProgressBar.Value = e.ProgressPercentage;
        allocUnitToolStripStatusLabel.Text = "Loading " + (string)e.UserState;
    }

    /// <summary>
    /// Handles the RunWorkerCompleted event of the AllocUnitBackgroundWorker control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
    private void AllocUnitBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        Cursor = Cursors.Arrow;

        allocUnitProgressBar.Visible = false;
        allocUnitToolStripStatusLabel.Text = string.Empty;

        if (e.Result == null)
        {
            return;
        }

        allocationContainer.Holding = false;
        allocationContainer.HoldingMessage = string.Empty;
        allocationContainer.IncludeIam = true;

        allocationContainer.ClearMapLayers();

        var layers = (List<AllocationLayer>)e.Result;

        foreach (var layer in layers)
        {
            allocationContainer.AddMapLayer(layer);
        }

        if (bufferPoolToolStripButton.Checked)
        {
            ShowBufferPool(true);
        }

        if (allocationContainer.Mode == MapMode.Full)
        {
            allocationContainer.ShowFittedMap();
        }

        ShowExtendedColumns(true);

        NameColumn.HeaderText = "Table";
        IndexNameColumn.HeaderText = "Index";

        allocationBindingSource.DataSource = layers;

        keysDataGridView.ClearSelection();

        ShowPfs(false);
    }

    /// <summary>
    /// Show or hide the PFS.
    /// </summary>
    /// <param name="show">if set to <c>true</c> [show].</param>
    private void ShowPfs(bool show)
    {
        if (show)
        {
            allocationContainer.Pfs = InternalsViewerConnection.CurrentConnection().CurrentDatabase.Pfs;
            allocationContainer.ExtentSize = AllocationMap.Large;
            allocationContainer.Mode = MapMode.Pfs;
        }
        else
        {
            allocationContainer.Mode = MapMode.Standard;
            allocationContainer.ExtentSize = AllocationMap.Small;

            if (allocationContainer.AllocationLayers.Count == 0)
            {
                DisplayAllocationUnitLayers();
            }
        }

        allocationContainer.Refresh();
    }

    /// <summary>
    /// Changes the size of the extent on the allocation maps
    /// </summary>
    private void ChangeExtentSize()
    {
        switch (extentSizeToolStripComboBox.SelectedItem.ToString())
        {
            case "Small":

                allocationContainer.Mode = MapMode.Standard;
                allocationContainer.ExtentSize = AllocationMap.Small;

                break;

            case "Medium":

                allocationContainer.Mode = MapMode.Standard;
                allocationContainer.ExtentSize = AllocationMap.Medium;

                break;

            case "Large":

                allocationContainer.Mode = MapMode.Standard;
                allocationContainer.ExtentSize = AllocationMap.Large;

                break;

            case "Fit":

                allocationContainer.Mode = MapMode.Full;

                allocationContainer.ShowFittedMap();

                break;
        }
    }

    /// <summary>
    /// Displays the buffer pool layer.
    /// </summary>
    private void DisplayBufferPoolLayer()
    {
        bufferPool.Refresh();

        var clean = new AllocationPage();

        clean.SinglePageSlots.AddRange(bufferPool.CleanPages);

        var bufferPoolLayer = new AllocationLayer("Buffer Pool", clean, Color.Black)
        {
            SingleSlotsOnly = true,
            Transparency = 80,
            Transparent = true,
            BorderColour = Color.WhiteSmoke,
            UseBorderColour = true,
            UseDefaultSinglePageColour = false
        };

        var dirty = new AllocationPage();

        dirty.SinglePageSlots.AddRange(bufferPool.DirtyPages);

        var bufferPoolDirtyLayer = new AllocationLayer("Buffer Pool (Dirty)", dirty, Color.IndianRed)
        {
            SingleSlotsOnly = true,
            Transparent = false,
            BorderColour = Color.WhiteSmoke,
            UseBorderColour = true,
            UseDefaultSinglePageColour = false,
            LayerType = AllocationLayerType.TopLeftCorner
        };

        allocationContainer.AddMapLayer(bufferPoolLayer);
        allocationContainer.AddMapLayer(bufferPoolDirtyLayer);
    }

    /// <summary>
    /// Shows the buffer pool.
    /// </summary>
    /// <param name="show">if set to <c>true</c> [show].</param>
    private void ShowBufferPool(bool show)
    {
        if (show)
        {
            DisplayBufferPoolLayer();
        }
        else
        {
            allocationContainer.RemoveLayer("Buffer Pool");
            allocationContainer.RemoveLayer("Buffer Pool (Dirty)");

            allocationContainer.Invalidate();
        }
    }

    /// <summary>
    /// Displays the allocation map layers (GAM, SGAM etc.)
    /// </summary>
    private void DisplayAllocationMapLayers()
    {
        allocationContainer.ClearMapLayers();

        if (gamToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("GAM (Inverted)",
                "Unavailable - (Uniform extent/full mixed extent)",
                Color.FromArgb(172, 186, 214),
                true,
                InternalsViewerConnection.CurrentConnection().CurrentDatabase.Gam);
        }

        if (sgamToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("SGAM",
                "Partially Unavailable - (Mixed extent with free pages)",
                Color.FromArgb(168, 204, 162),
                false,
                InternalsViewerConnection.CurrentConnection().CurrentDatabase.SGam);
        }

        if (dcmToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("DCM",
                "Differential Change Map",
                Color.FromArgb(120, 150, 150),
                false,
                InternalsViewerConnection.CurrentConnection().CurrentDatabase.Dcm);
        }

        if (bcmToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("BCM Allocated",
                "Bulk Change Map",
                Color.FromArgb(150, 120, 150),
                false,
                InternalsViewerConnection.CurrentConnection().CurrentDatabase.Bcm);
        }

        ShowExtendedColumns(false);

        NameColumn.HeaderText = "Layer";
        IndexNameColumn.HeaderText = "Description";

        allocationBindingSource.DataSource = allocationContainer.AllocationLayers;

        keysDataGridView.ClearSelection();

        ShowPfs(false);
    }

    /// <summary>
    /// Shows the extended key columns
    /// </summary>
    /// <param name="visible">if set to <c>true</c> [visible].</param>
    private void ShowExtendedColumns(bool visible)
    {
        IndexTypeColumn.Visible = visible;
        TotalPagesColumn.Visible = visible;
        UsedPagesColumn.Visible = visible;
    }

    /// <summary>
    /// Adds the database allocation for each file in the database
    /// </summary>
    /// <param name="layerName">Name of the layer.</param>
    /// <param name="description">The description.</param>
    /// <param name="layerColour">The layer colour.</param>
    /// <param name="invert">if set to <c>true</c> [invert].</param>
    /// <param name="allocation">The allocation.</param>
    private void AddDatabaseAllocation(string layerName,
        string description,
        Color layerColour,
        bool invert,
        IDictionary<int, Allocation> allocation)
    {
        foreach (var fileId in allocation.Keys)
        {
            allocationContainer.AddMapLayer(new AllocationLayer(allocation[fileId])
            {
                Name = layerName,
                ObjectName = layerName,
                IndexName = description,
                Invert = invert,
                Colour = layerColour
            });
        }
    }

    /// <summary>
    /// Called when a connection request is made.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    internal virtual void OnConnect(object sender, EventArgs e)
    {
        if (Connect != null)
        {
            Connect(sender, e);
        }
    }

    /// <summary>
    /// Called when a page is requested to be viewed
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
    internal virtual void OnViewPage(object sender, PageEventArgs e)
    {
        if (ViewPage != null)
        {
            ViewPage(sender, e);
        }
    }

    /// <summary>
    /// Handles the SelectedIndexChanged event of the DatabaseToolStripComboBox control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void DatabaseToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        InternalsViewerConnection.CurrentConnection().CurrentDatabase = (Database)databaseToolStripComboBox.SelectedItem;

        LoadDatabase();
    }

    /// <summary>
    /// Handles the SelectedIndexChanged event of the ExtentSizeToolStripComboBox control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void ExtentSizeToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        ChangeExtentSize();
    }

    /// <summary>
    /// Handles the Click event of the PfsToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    //private void PfsToolStripButton_Click(object sender, EventArgs e)
    //{
    //    this.ShowPfs(this.pfsToolStripButton.Checked);
    //}

    /// <summary>
    /// Handles the Click event of the BufferPoolToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void BufferPoolToolStripButton_Click(object sender, EventArgs e)
    {
        ShowBufferPool(bufferPoolToolStripButton.Checked);
    }

    /// <summary>
    /// Handles the PageOver event of the AllocationContainer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
    private void AllocationContainer_PageOver(object sender, PageEventArgs e)
    {
        AllocUnitLabel.Text = string.Empty;

        pageAddressToolStripStatusLabel.Text = e.Address.ToString();

        switch (allocationContainer.Mode)
        {
            case MapMode.Standard:

                if (e.Address.PageId % Database.PfsInterval == 0 || e.Address.PageId == 1)
                {
                    AllocUnitLabel.Text = "PFS";
                }

                if (e.Address.PageId % Database.AllocationInterval < 8)
                {
                    switch (e.Address.PageId % Database.AllocationInterval)
                    {
                        case 0:

                            if (e.Address.PageId == 0)
                            {
                                AllocUnitLabel.Text = "File Header";
                            }

                            break;

                        case 1:

                            AllocUnitLabel.Text = "GAM";
                            break;

                        case 2:

                            AllocUnitLabel.Text = "SGAM";
                            break;

                        case 6:

                            AllocUnitLabel.Text = "DCM";
                            break;

                        case 7:

                            AllocUnitLabel.Text = "BCM";
                            break;

                        case 9:

                            AllocUnitLabel.Text = "Boot Page";
                            break;
                    }
                }

                var layers = AllocationLayer.FindPage(e.Address, allocationContainer.AllocationLayers);

                foreach (var name in layers)
                {
                    if (AllocUnitLabel.Text != string.Empty)
                    {
                        AllocUnitLabel.Text += " | ";
                    }

                    AllocUnitLabel.Text += name;
                }

                break;

            case MapMode.Pfs:

                AllocUnitLabel.Text = allocationContainer.PagePfsByte(e.Address).ToString();
                break;
        }
    }

    /// <summary>
    /// Handles the Click event of the FileDetailsToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void FileDetailsToolStripButton_Click(object sender, EventArgs e)
    {
        allocationContainer.CreateAllocationMaps(allocationContainer.AllocationMaps);

        LoadDatabase();
    }

    /// <summary>
    /// Handles the SelectionChanged event of the KeysDataGridView control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void KeysDataGridView_SelectionChanged(object sender, EventArgs e)
    {
        keyChanging = true;

        if (keysDataGridView.SelectedRows.Count > 0)
        {
            foreach (var map in allocationContainer.AllocationMaps.Values)
            {
                foreach (var layer in map.MapLayers)
                {
                    if (layer.Name != "Buffer Pool")
                    {
                        var name = (string)keysDataGridView.SelectedRows[0].Cells[1].Value;
                        var indexName = (string)keysDataGridView.SelectedRows[0].Cells[2].Value;

                        layer.Transparent = !(layer.Name == name && layer.IndexName == indexName);
                    }
                }

                map.Invalidate();
            }
        }
        else
        {
            foreach (var map in allocationContainer.AllocationMaps.Values)
            {
                foreach (var layer in map.MapLayers)
                {
                    if (layer.Name != "Buffer Pool")
                    {
                        layer.Transparent = false;
                    }
                }

                map.Invalidate();
            }
        }

        keysDataGridView.Invalidate();
    }

    /// <summary>
    /// Handles the CellClick event of the KeysDataGridView control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Forms.DataGridViewCellEventArgs"/> instance containing the event data.</param>
    private void KeysDataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (!keyChanging)
        {
            if (keysDataGridView.SelectedRows.Count > 0)
            {
                keysDataGridView.ClearSelection();
            }
        }

        keyChanging = false;
    }

    /// <summary>
    /// Handles the PageClicked event of the AllocationContainer control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
    private void AllocationContainer_PageClicked(object sender, PageEventArgs e)
    {
        OnViewPage(sender, e);
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
                OnViewPage(this, new PageEventArgs(new RowIdentifier(PageAddress.Parse(pageToolStripTextBox.Text), 0), e.Shift));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
            }
        }
    }

    /// <summary>
    /// Handles the Click event of the ShowKeyToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void ShowKeyToolStripButton_Click(object sender, EventArgs e)
    {
        splitContainer.Panel2Collapsed = !showKeyToolStripButton.Checked;
    }

    /// <summary>
    /// Handles the CheckedChanged event of the FileDetailsToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void FileDetailsToolStripButton_CheckedChanged(object sender, EventArgs e)
    {
        if (fileDetailsToolStripButton.Enabled)
        {
            allocationContainer.ShowFileInformation = fileDetailsToolStripButton.Checked;
        }
    }

    /// <summary>
    /// Handles the Click event of the AllocationUnitsToolStripMenuItem control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void AllocationUnitsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        mapToolStripButton.Image = (sender as ToolStripMenuItem).Image;
        mapToolStripButton.Text = AllocationUnitsText;

        CancelWorkerAndWait(allocUnitBackgroundWorker);

        DisplayAllocationUnitLayers();
    }

    /// <summary>
    /// Handles the Click event of the AllocationMapsToolStripMenuItem control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void AllocationMapsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        CancelWorkerAndWait(allocUnitBackgroundWorker);

        mapToolStripButton.Image = (sender as ToolStripMenuItem).Image;
        mapToolStripButton.Text = AllocationMapText;

        mapToolStripButton.HideDropDown();

        DisplayAllocationMapLayers();
    }

    private void pFSToolStripMenuItem_Click(object sender, EventArgs e)
    {
        mapToolStripButton.Image = (sender as ToolStripMenuItem).Image;
        mapToolStripButton.Text = PageFreeSpaceText;

        ShowPfs(true);
    }
}