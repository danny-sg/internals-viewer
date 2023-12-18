using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.UI.Allocations;

#pragma warning disable CA1416

namespace InternalsViewer.UI;

public partial class AllocationWindow : UserControl
{
    public IDatabaseService DatabaseService { get; }

    public event EventHandler Connect;
    public event EventHandler<PageEventArgs> ViewPage;

    protected delegate void LoadDatabaseDelegate();
    private readonly BufferPool bufferPool = new(new(), new());
    private bool keyChanging;
    private const string AllocationMapText = "Allocation Map";
    private const string AllocationUnitsText = "Allocation Units";
    private const string PageFreeSpaceText = "PFS";

    public DatabaseDetail? CurrentDatabase { get; set; }

    public AllocationWindow(IDatabaseService databaseService)
    {
        DatabaseService = databaseService;
        InitializeComponent();

        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.DoubleBuffer, true);

        extentSizeToolStripComboBox.SelectedIndex = 0;
    }

    public List<DatabaseSummary> Databases { get; set; } = new();

    private void EnableToolbar(bool enabled)
    {
        databaseToolStripComboBox.Enabled = enabled;
        extentSizeToolStripComboBox.Enabled = enabled;
        bufferPoolToolStripButton.Enabled = enabled;
        fileDetailsToolStripButton.Enabled = enabled;
        showKeyToolStripButton.Enabled = enabled;
        mapToolStripButton.Enabled = enabled;
    }

    public void RefreshDatabases()
    {
        if (databaseToolStripComboBox.ComboBox != null)
        {
            databaseToolStripComboBox.ComboBox.DataSource = null;

            databaseToolStripComboBox.ComboBox.Items.Clear();

            EnableToolbar(Databases.Count > 0);

            databaseToolStripComboBox.ComboBox.DataSource = Databases;
            databaseToolStripComboBox.ComboBox.DisplayMember = "Name";
            databaseToolStripComboBox.ComboBox.ValueMember = "DatabaseId";
        }
    }

    private void LoadDatabase()
    {
        if (allocationContainer.InvokeRequired)
        {
            Invoke(new LoadDatabaseDelegate(LoadDatabase));
        }
        else
        {
            if (databaseToolStripComboBox.ComboBox != null && databaseToolStripComboBox.ComboBox.SelectedItem != CurrentDatabase)
            {
                databaseToolStripComboBox.ComboBox.SelectedItem = CurrentDatabase;
            }

            if (CurrentDatabase != null)
            {
                allocationContainer.CurrentDatabase = CurrentDatabase;

                allocationContainer.CreateAllocationMaps(CurrentDatabase.Files);
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

    private void DisplayAllocationUnitLayers()
    {
        allocationContainer.IncludeIam = false;
        allocationContainer.ClearMapLayers();

        var unallocated = new AllocationLayer();

        unallocated.Name = "Available - (Unused)";
        unallocated.Colour = Color.Gainsboro;
        unallocated.IsVisible = false;

        allocationContainer.AddMapLayer(unallocated);

        CancelWorkerAndWait(allocUnitBackgroundWorker);

        allocUnitBackgroundWorker.RunWorkerAsync();
    }

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

    private void AllocUnitBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
    {
        e.Result = AllocationUnitsLayer.GenerateLayers(CurrentDatabase!, (BackgroundWorker)sender, true, true);
    }

    private void AllocUnitBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        allocUnitProgressBar.Value = e.ProgressPercentage;
        allocUnitToolStripStatusLabel.Text = "Loading " + (string)e.UserState;
    }

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

    private void ShowPfs(bool show)
    {
        if(CurrentDatabase==null)
        {
            return;
        }

        if (show)
        {
            allocationContainer.Pfs = CurrentDatabase.Pfs;
            allocationContainer.ExtentSize = AllocationMap.Large;
            allocationContainer.Mode = MapMode.Pfs;
        }
        else
        {
            allocationContainer.Mode = MapMode.Standard;
            allocationContainer.ExtentSize = AllocationMap.Small;

            if (allocationContainer.AllocationLayers.Count == 0)
            {
                // DisplayAllocationUnitLayers();
            }
        }

        allocationContainer.Refresh();
    }

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

    private void DisplayBufferPoolLayer()
    {
        //bufferPool.Refresh();

        var clean = new AllocationPage();

        clean.SinglePageSlots.AddRange(bufferPool.CleanPages);

        var bufferPoolLayer = new AllocationLayer("Buffer Pool", clean, Color.Black)
        {
            SingleSlotsOnly = true,
            Transparency = 80,
            IsTransparent = true,
            BorderColour = Color.WhiteSmoke,
            UseBorderColour = true,
            UseDefaultSinglePageColour = false
        };

        var dirty = new AllocationPage();

        dirty.SinglePageSlots.AddRange(bufferPool.DirtyPages);

        var bufferPoolDirtyLayer = new AllocationLayer("Buffer Pool (Dirty)", dirty, Color.IndianRed)
        {
            SingleSlotsOnly = true,
            IsTransparent = false,
            BorderColour = Color.WhiteSmoke,
            UseBorderColour = true,
            UseDefaultSinglePageColour = false,
            LayerType = AllocationLayerType.TopLeftCorner
        };

        allocationContainer.AddMapLayer(bufferPoolLayer);
        allocationContainer.AddMapLayer(bufferPoolDirtyLayer);
    }

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

        if(CurrentDatabase==null)
        {
            return;
        }

        if (gamToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("GAM (Inverted)",
                "Unavailable - (Uniform extent/full mixed extent)",
                Color.FromArgb(172, 186, 214),
                true,
                CurrentDatabase.Gam);
        }

        if (sgamToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("SGAM",
                "Partially Unavailable - (Mixed extent with free pages)",
                Color.FromArgb(168, 204, 162),
                false,
                CurrentDatabase.SGam);
        }

        if (dcmToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("DCM",
                "Differential Change Map",
                Color.FromArgb(120, 150, 150),
                false,
                CurrentDatabase.Dcm);
        }

        if (bcmToolStripMenuItem.Checked)
        {
            AddDatabaseAllocation("BCM Allocated",
                "Bulk Change Map",
                Color.FromArgb(150, 120, 150),
                false,
                CurrentDatabase.Bcm);
        }

        ShowExtendedColumns(false);

        NameColumn.HeaderText = "Layer";
        IndexNameColumn.HeaderText = "Description";

        allocationBindingSource.DataSource = allocationContainer.AllocationLayers;

        keysDataGridView.ClearSelection();

        ShowPfs(false);
    }

    private void ShowExtendedColumns(bool visible)
    {
        IndexTypeColumn.Visible = visible;
        TotalPagesColumn.Visible = visible;
        UsedPagesColumn.Visible = visible;
    }

    private void AddDatabaseAllocation(string layerName,
                                       string description,
                                       Color layerColour,
                                       bool invert,
                                       IDictionary<int, AllocationChain> allocation)
    {
        foreach (var fileId in allocation.Keys)
        {
            allocationContainer.AddMapLayer(new AllocationLayer(allocation[fileId])
            {
                Name = layerName,
                ObjectName = layerName,
                IndexName = description,
                IsInverted = invert,
                Colour = layerColour
            });
        }
    }

    internal virtual void OnConnect(object sender, EventArgs e)
    {
        if (Connect != null)
        {
            Connect(sender, e);
        }
    }

    internal virtual void OnViewPage(object sender, PageEventArgs e)
    {
        if (ViewPage != null)
        {
            ViewPage(sender, e);
        }
    }

    private async void DatabaseToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var databaseInfo = (DatabaseSummary)databaseToolStripComboBox.SelectedItem;

        if(databaseInfo==null)
        {
            return;
        }

        var database = await DatabaseService.Load(databaseInfo.Name);

        CurrentDatabase = database;

        LoadDatabase();
    }

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

    private void BufferPoolToolStripButton_Click(object sender, EventArgs e)
    {
        ShowBufferPool(bufferPoolToolStripButton.Checked);
    }

    private void AllocationContainer_PageOver(object sender, PageEventArgs e)
    {
        AllocUnitLabel.Text = string.Empty;

        pageAddressToolStripStatusLabel.Text = e.Address.ToString();

        switch (allocationContainer.Mode)
        {
            case MapMode.Standard:

                if (e.Address.PageId % PfsPage.PfsInterval == 0 || e.Address.PageId == 1)
                {
                    AllocUnitLabel.Text = "PFS";
                }

                if (e.Address.PageId % (AllocationPage.AllocationInterval * 8) < 8)
                {
                    switch (e.Address.PageId % (AllocationPage.AllocationInterval * 8))
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

    private void FileDetailsToolStripButton_Click(object sender, EventArgs e)
    {
        allocationContainer.CreateAllocationMaps(allocationContainer.AllocationMaps);

        LoadDatabase();
    }

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

                        layer.IsTransparent = !(layer.Name == name && layer.IndexName == indexName);
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
                        layer.IsTransparent = false;
                    }
                }

                map.Invalidate();
            }
        }

        keysDataGridView.Invalidate();
    }

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

    private void AllocationContainer_PageClicked(object sender, PageEventArgs e)
    {
        OnViewPage(sender, e);
    }

    private void PageToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Return)
        {
            try
            {
                OnViewPage(this, new PageEventArgs(new RowIdentifier(PageAddressParser.Parse(pageToolStripTextBox.Text), 0), e.Shift));
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
            }
        }
    }

    private void ShowKeyToolStripButton_Click(object sender, EventArgs e)
    {
        splitContainer.Panel2Collapsed = !showKeyToolStripButton.Checked;
    }

    private void FileDetailsToolStripButton_CheckedChanged(object sender, EventArgs e)
    {
        if (fileDetailsToolStripButton.Enabled)
        {
            allocationContainer.ShowFileInformation = fileDetailsToolStripButton.Checked;
        }
    }

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