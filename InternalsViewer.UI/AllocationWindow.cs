using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Allocations;

namespace InternalsViewer.UI
{
    public partial class AllocationWindow : UserControl
    {
        public event EventHandler Connect;
        public event EventHandler<PageEventArgs> ViewPage;

        protected delegate void LoadDatabaseDelegate();
        private readonly BufferPool bufferPool = new BufferPool();
        private bool keyChanging;
        private const string AllocationMapText = "Allocation Map";
        private const string AllocationUnitsText = "Allocation Units";
        private const string PageFreeSpaceText = "PFS";

        public AllocationWindow()
        {
            InitializeComponent();

            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.DoubleBuffer, true);

            extentSizeToolStripComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Enables buttons on the toolbar.
        /// </summary>
        /// <param name="enabled">if set to <c>true</c> [enabled].</param>
        private void EnableToolbar(bool enabled)
        {
            this.databaseToolStripComboBox.Enabled = enabled;
            this.extentSizeToolStripComboBox.Enabled = enabled;
            this.bufferPoolToolStripButton.Enabled = enabled;
            this.fileDetailsToolStripButton.Enabled = enabled;
            this.showKeyToolStripButton.Enabled = enabled;
            this.mapToolStripButton.Enabled = enabled;
        }

        /// <summary>
        /// Refreshes the databases list
        /// </summary>
        public void RefreshDatabases()
        {
            this.databaseToolStripComboBox.ComboBox.DataSource = null;

            this.databaseToolStripComboBox.ComboBox.Items.Clear();

            this.EnableToolbar(InternalsViewerConnection.CurrentConnection().Databases.Count > 0);

            this.databaseToolStripComboBox.ComboBox.DataSource = InternalsViewerConnection.CurrentConnection().Databases;
            this.databaseToolStripComboBox.ComboBox.DisplayMember = "Name";
            this.databaseToolStripComboBox.ComboBox.ValueMember = "DatabaseId";
        }

        /// <summary>
        /// Loads the selected database.
        /// </summary>
        private void LoadDatabase()
        {
            if (this.allocationContainer.InvokeRequired)
            {
                this.Invoke(new LoadDatabaseDelegate(this.LoadDatabase));
            }
            else
            {
                if (this.databaseToolStripComboBox.ComboBox.SelectedItem != InternalsViewerConnection.CurrentConnection().CurrentDatabase)
                {
                    this.databaseToolStripComboBox.ComboBox.SelectedItem = InternalsViewerConnection.CurrentConnection().CurrentDatabase;
                }

                if (InternalsViewerConnection.CurrentConnection().CurrentDatabase != null)
                {
                    this.allocationContainer.CreateAllocationMaps(InternalsViewerConnection.CurrentConnection().CurrentDatabase.Files);
                }

                this.CancelWorkerAndWait(allocUnitBackgroundWorker);

                if (this.mapToolStripButton.Text == AllocationMapText)
                {
                    this.DisplayAllocationMapLayers();
                }
                else
                {
                    this.DisplayAllocationUnitLayers();
                }
            }
        }

        /// <summary>
        /// Displays the allocation layers.
        /// </summary>
        private void DisplayAllocationUnitLayers()
        {
            this.allocationContainer.IncludeIam = false;
            this.allocationContainer.ClearMapLayers();

            AllocationLayer unallocated = new AllocationLayer();

            unallocated.Name = "Available - (Unused)";
            unallocated.Colour = Color.Gainsboro;
            unallocated.Visible = false;

            this.allocationContainer.AddMapLayer(unallocated);

            this.CancelWorkerAndWait(allocUnitBackgroundWorker);

            this.allocUnitBackgroundWorker.RunWorkerAsync();
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
            this.allocUnitProgressBar.Value = e.ProgressPercentage;
            this.allocUnitToolStripStatusLabel.Text = "Loading " + (string)e.UserState;
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the AllocUnitBackgroundWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void AllocUnitBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Cursor = Cursors.Arrow;

            this.allocUnitProgressBar.Visible = false;
            this.allocUnitToolStripStatusLabel.Text = string.Empty;

            if (e.Result == null)
            {
                return;
            }

            this.allocationContainer.Holding = false;
            this.allocationContainer.HoldingMessage = string.Empty;
            this.allocationContainer.IncludeIam = true;

            this.allocationContainer.ClearMapLayers();

            List<AllocationLayer> layers = (List<AllocationLayer>)e.Result;

            foreach (AllocationLayer layer in layers)
            {
                this.allocationContainer.AddMapLayer(layer);
            }

            if (this.bufferPoolToolStripButton.Checked)
            {
                this.ShowBufferPool(true);
            }

            if (this.allocationContainer.Mode == MapMode.Full)
            {
                this.allocationContainer.ShowFittedMap();
            }

            this.ShowExtendedColumns(true);

            this.NameColumn.HeaderText = "Table";
            this.IndexNameColumn.HeaderText = "Index";

            this.allocationBindingSource.DataSource = layers;

            this.keysDataGridView.ClearSelection();

            this.ShowPfs(false);
        }

        /// <summary>
        /// Show or hide the PFS.
        /// </summary>
        /// <param name="show">if set to <c>true</c> [show].</param>
        private void ShowPfs(bool show)
        {
            if (show)
            {
                this.allocationContainer.Pfs = InternalsViewerConnection.CurrentConnection().CurrentDatabase.Pfs;
                this.allocationContainer.ExtentSize = AllocationMap.Large;
                this.allocationContainer.Mode = MapMode.Pfs;
            }
            else
            {
                this.allocationContainer.Mode = MapMode.Standard;
                this.allocationContainer.ExtentSize = AllocationMap.Small;

                if (this.allocationContainer.AllocationLayers.Count == 0)
                {
                    this.DisplayAllocationUnitLayers();
                }
            }

            this.allocationContainer.Refresh();
        }

        /// <summary>
        /// Changes the size of the extent on the allocation maps
        /// </summary>
        private void ChangeExtentSize()
        {
            switch (this.extentSizeToolStripComboBox.SelectedItem.ToString())
            {
                case "Small":

                    this.allocationContainer.Mode = MapMode.Standard;
                    this.allocationContainer.ExtentSize = AllocationMap.Small;

                    break;

                case "Medium":

                    this.allocationContainer.Mode = MapMode.Standard;
                    this.allocationContainer.ExtentSize = AllocationMap.Medium;

                    break;

                case "Large":

                    this.allocationContainer.Mode = MapMode.Standard;
                    this.allocationContainer.ExtentSize = AllocationMap.Large;

                    break;

                case "Fit":

                    this.allocationContainer.Mode = MapMode.Full;

                    this.allocationContainer.ShowFittedMap();

                    break;
            }
        }

        /// <summary>
        /// Displays the buffer pool layer.
        /// </summary>
        private void DisplayBufferPoolLayer()
        {
            this.bufferPool.Refresh();

            AllocationPage clean = new AllocationPage();

            clean.SinglePageSlots.AddRange(this.bufferPool.CleanPages);

            AllocationLayer bufferPoolLayer = new AllocationLayer("Buffer Pool", clean, Color.Black)
                                                {
                                                    SingleSlotsOnly = true,
                                                    Transparency = 80,
                                                    Transparent = true,
                                                    BorderColour = Color.WhiteSmoke,
                                                    UseBorderColour = true,
                                                    UseDefaultSinglePageColour = false
                                                };

            AllocationPage dirty = new AllocationPage();

            dirty.SinglePageSlots.AddRange(this.bufferPool.DirtyPages);

            AllocationLayer bufferPoolDirtyLayer = new AllocationLayer("Buffer Pool (Dirty)", dirty, Color.IndianRed)
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
                this.DisplayBufferPoolLayer();
            }
            else
            {
                this.allocationContainer.RemoveLayer("Buffer Pool");
                this.allocationContainer.RemoveLayer("Buffer Pool (Dirty)");

                this.allocationContainer.Invalidate();
            }
        }

        /// <summary>
        /// Displays the allocation map layers (GAM, SGAM etc.)
        /// </summary>
        private void DisplayAllocationMapLayers()
        {
            this.allocationContainer.ClearMapLayers();

            if (this.gamToolStripMenuItem.Checked)
            {
                this.AddDatabaseAllocation("GAM (Inverted)",
                                           "Unavailable - (Uniform extent/full mixed extent)",
                                           Color.FromArgb(172, 186, 214),
                                           true,
                                           InternalsViewerConnection.CurrentConnection().CurrentDatabase.Gam);
            }

            if (this.sgamToolStripMenuItem.Checked)
            {
                this.AddDatabaseAllocation("SGAM",
                                           "Partially Unavailable - (Mixed extent with free pages)",
                                           Color.FromArgb(168, 204, 162),
                                           false,
                                           InternalsViewerConnection.CurrentConnection().CurrentDatabase.SGam);
            }

            if (this.dcmToolStripMenuItem.Checked)
            {
                this.AddDatabaseAllocation("DCM",
                                           "Differential Change Map",
                                           Color.FromArgb(120, 150, 150),
                                           false,
                                           InternalsViewerConnection.CurrentConnection().CurrentDatabase.Dcm);
            }

            if (this.bcmToolStripMenuItem.Checked)
            {
                this.AddDatabaseAllocation("BCM Allocated",
                                           "Bulk Change Map",
                                           Color.FromArgb(150, 120, 150),
                                           false,
                                           InternalsViewerConnection.CurrentConnection().CurrentDatabase.Bcm);
            }

            this.ShowExtendedColumns(false);

            this.NameColumn.HeaderText = "Layer";
            this.IndexNameColumn.HeaderText = "Description";

            this.allocationBindingSource.DataSource = this.allocationContainer.AllocationLayers;

            this.keysDataGridView.ClearSelection();

            this.ShowPfs(false);
        }

        /// <summary>
        /// Shows the extended key columns
        /// </summary>
        /// <param name="visible">if set to <c>true</c> [visible].</param>
        private void ShowExtendedColumns(bool visible)
        {
            this.IndexTypeColumn.Visible = visible;
            this.TotalPagesColumn.Visible = visible;
            this.UsedPagesColumn.Visible = visible;
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
            foreach (int fileId in allocation.Keys)
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
            if (this.Connect != null)
            {
                this.Connect(sender, e);
            }
        }

        /// <summary>
        /// Called when a page is requested to be viewed
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        internal virtual void OnViewPage(object sender, PageEventArgs e)
        {
            if (this.ViewPage != null)
            {
                this.ViewPage(sender, e);
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

            this.LoadDatabase();
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the ExtentSizeToolStripComboBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void ExtentSizeToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ChangeExtentSize();
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
            this.ShowBufferPool(this.bufferPoolToolStripButton.Checked);
        }

        /// <summary>
        /// Handles the PageOver event of the AllocationContainer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        private void AllocationContainer_PageOver(object sender, PageEventArgs e)
        {
            this.AllocUnitLabel.Text = string.Empty;

            this.pageAddressToolStripStatusLabel.Text = e.Address.ToString();

            switch (allocationContainer.Mode)
            {
                case MapMode.Standard:

                    if (e.Address.PageId % Database.PfsInterval == 0 || e.Address.PageId == 1)
                    {
                        this.AllocUnitLabel.Text = "PFS";
                    }

                    if (e.Address.PageId % Database.AllocationInterval < 8)
                    {
                        switch (e.Address.PageId % Database.AllocationInterval)
                        {
                            case 0:

                                if (e.Address.PageId == 0)
                                {
                                    this.AllocUnitLabel.Text = "File Header";
                                }

                                break;

                            case 1:

                                this.AllocUnitLabel.Text = "GAM";
                                break;

                            case 2:

                                this.AllocUnitLabel.Text = "SGAM";
                                break;

                            case 6:

                                this.AllocUnitLabel.Text = "DCM";
                                break;

                            case 7:

                                this.AllocUnitLabel.Text = "BCM";
                                break;

                            case 9:

                                this.AllocUnitLabel.Text = "Boot Page";
                                break;
                        }
                    }

                    List<string> layers = AllocationLayer.FindPage(e.Address, allocationContainer.AllocationLayers);

                    foreach (string name in layers)
                    {
                        if (this.AllocUnitLabel.Text != string.Empty)
                        {
                            this.AllocUnitLabel.Text += " | ";
                        }

                        this.AllocUnitLabel.Text += name;
                    }

                    break;

                case MapMode.Pfs:

                    this.AllocUnitLabel.Text = this.allocationContainer.PagePfsByte(e.Address).ToString();
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
            this.allocationContainer.CreateAllocationMaps(this.allocationContainer.AllocationMaps);

            this.LoadDatabase();
        }

        /// <summary>
        /// Handles the SelectionChanged event of the KeysDataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void KeysDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            this.keyChanging = true;

            if (keysDataGridView.SelectedRows.Count > 0)
            {
                foreach (AllocationMap map in allocationContainer.AllocationMaps.Values)
                {
                    foreach (AllocationLayer layer in map.MapLayers)
                    {
                        if (layer.Name != "Buffer Pool")
                        {
                            string name = (string)keysDataGridView.SelectedRows[0].Cells[1].Value;
                            string indexName = (string)keysDataGridView.SelectedRows[0].Cells[2].Value;

                            layer.Transparent = !(layer.Name == name && layer.IndexName == indexName);
                        }
                    }

                    map.Invalidate();
                }
            }
            else
            {
                foreach (AllocationMap map in allocationContainer.AllocationMaps.Values)
                {
                    foreach (AllocationLayer layer in map.MapLayers)
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
            if (!this.keyChanging)
            {
                if (this.keysDataGridView.SelectedRows.Count > 0)
                {
                    this.keysDataGridView.ClearSelection();
                }
            }

            this.keyChanging = false;
        }

        /// <summary>
        /// Handles the PageClicked event of the AllocationContainer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        private void AllocationContainer_PageClicked(object sender, PageEventArgs e)
        {
            this.OnViewPage(sender, e);
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
                    this.OnViewPage(this, new PageEventArgs(new RowIdentifier(PageAddress.Parse(pageToolStripTextBox.Text), 0), e.Shift));
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
            if (this.fileDetailsToolStripButton.Enabled)
            {
                allocationContainer.ShowFileInformation = this.fileDetailsToolStripButton.Checked;
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

            this.CancelWorkerAndWait(allocUnitBackgroundWorker);

            this.DisplayAllocationUnitLayers();
        }

        /// <summary>
        /// Handles the Click event of the AllocationMapsToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void AllocationMapsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.CancelWorkerAndWait(allocUnitBackgroundWorker);

            mapToolStripButton.Image = (sender as ToolStripMenuItem).Image;
            mapToolStripButton.Text = AllocationMapText;

            mapToolStripButton.HideDropDown();

            this.DisplayAllocationMapLayers();
        }

        private void pFSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mapToolStripButton.Image = (sender as ToolStripMenuItem).Image;
            mapToolStripButton.Text = PageFreeSpaceText;

            this.ShowPfs(true);
        }
    }
}
