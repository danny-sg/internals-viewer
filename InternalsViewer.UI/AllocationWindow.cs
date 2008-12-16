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

        public AllocationWindow()
        {
            InitializeComponent();

            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.DoubleBuffer, true);

            extentSizeToolStripComboBox.SelectedIndex = 0;
        }

        internal virtual void OnConnect(object sender, EventArgs e)
        {
            if (this.Connect != null)
            {
                this.Connect(sender, e);
            }
        }

        internal virtual void OnViewPage(object sender, PageEventArgs e)
        {
            if (this.ViewPage != null)
            {
                this.ViewPage(sender, e);
            }
        }

        private void EnableToolbar(bool enabled)
        {
            this.databaseToolStripComboBox.Enabled = enabled;
            this.extentSizeToolStripComboBox.Enabled = enabled;
            this.bufferPoolToolStripButton.Enabled = enabled;
            this.pfsToolStripButton.Enabled = enabled;
            this.fileDetailsToolStripButton.Enabled = enabled;
            this.showKeyToolStripButton.Enabled = enabled;
        }

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
            if (this.allocationContainer.InvokeRequired)
            {
                this.Invoke(new LoadDatabaseDelegate(this.LoadDatabase));
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

                this.CancelWorkerAndWait(allocUnitBackgroundWorker);

                this.DisplayLayers();
            }
        }

        /// <summary>
        /// Displays the allocation layers.
        /// </summary>
        private void DisplayLayers()
        {
            allocationContainer.IncludeIam = false;
            allocationContainer.ClearMapLayers();

            AllocationLayer unallocated = new AllocationLayer();

            unallocated.Name = "Available - (Unused)";
            unallocated.Colour = Color.Gainsboro;
            unallocated.Visible = false;

            allocationContainer.AddMapLayer(unallocated);

            this.CancelWorkerAndWait(allocUnitBackgroundWorker);

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

        private void AllocUnitBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = AllocationUnitsLayer.GenerateLayers(InternalsViewerConnection.CurrentConnection().CurrentDatabase, (BackgroundWorker)sender, true, false);
        }

        private void AllocUnitBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.allocUnitProgressBar.Value = e.ProgressPercentage;
            this.allocUnitToolStripStatusLabel.Text = "Loading " + (string)e.UserState;
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

            this.allocationBindingSource.DataSource = layers;

            this.keysDataGridView.ClearSelection();
        }

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
                    DisplayLayers();
                }
            }

            allocationContainer.Refresh();
        }

        private void DatabaseToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            InternalsViewerConnection.CurrentConnection().CurrentDatabase = (Database)databaseToolStripComboBox.SelectedItem;

            this.LoadDatabase();
        }

        private void ExtentSizeToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ChangeExtentSize();
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

        /// <summary>
        /// Displays the buffer pool layer.
        /// </summary>
        private void DisplayBufferPoolLayer()
        {
            this.bufferPool.Refresh();

            AllocationPage clean = new AllocationPage();

            clean.SinglePageSlots.AddRange(this.bufferPool.CleanPages);

            AllocationLayer bufferPoolLayer = new AllocationLayer("Buffer Pool", clean, Color.Black);
            bufferPoolLayer.SingleSlotsOnly = true;
            bufferPoolLayer.Transparency = 80;
            bufferPoolLayer.Transparent = true;
            bufferPoolLayer.BorderColour = Color.WhiteSmoke;
            bufferPoolLayer.UseBorderColour = true;
            bufferPoolLayer.UseDefaultSinglePageColour = false;

            AllocationPage dirty = new AllocationPage();

            dirty.SinglePageSlots.AddRange(this.bufferPool.DirtyPages);

            AllocationLayer bufferPoolDirtyLayer = new AllocationLayer("Buffer Pool (Dirty)", dirty, Color.IndianRed);

            bufferPoolDirtyLayer.SingleSlotsOnly = true;
            bufferPoolDirtyLayer.Transparent = false;

            bufferPoolDirtyLayer.BorderColour = Color.WhiteSmoke;
            bufferPoolDirtyLayer.UseBorderColour = true;
            bufferPoolDirtyLayer.UseDefaultSinglePageColour = false;
            bufferPoolDirtyLayer.LayerType = AllocationLayerType.TopLeftCorner;

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
                allocationContainer.RemoveLayer("Buffer Pool");
                allocationContainer.RemoveLayer("Buffer Pool (Dirty)");

                allocationContainer.Invalidate();
            }
        }

        private void PfsToolStripButton_Click(object sender, EventArgs e)
        {
            ShowPfs(pfsToolStripButton.Checked);
        }

        private void BufferPoolToolStripButton_Click(object sender, EventArgs e)
        {
            ShowBufferPool(this.bufferPoolToolStripButton.Checked);
        }

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

                    List<string> layers = AllocationLayer.FindPage(e.Address, allocationContainer.AllocationLayers);

                    foreach (string name in layers)
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
            //this.allocationContainer.ShowFileInformation = fileDetailsToolStripButton.Checked;
            this.allocationContainer.CreateAllocationMaps(this.allocationContainer.AllocationMaps);
            LoadDatabase();
        }

        private void KeysDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            keyChanging = true;

            if (keysDataGridView.SelectedRows.Count > 0)
            {
                foreach (AllocationMap map in allocationContainer.AllocationMaps.Values)
                {
                    foreach (AllocationLayer layer in map.MapLayers)
                    {
                        if (layer.Name != ("Buffer Pool"))
                        {
                            layer.Transparent = !(layer.Name == keysDataGridView.SelectedRows[0].Cells[1].Value.ToString() && layer.IndexName == keysDataGridView.SelectedRows[0].Cells[2].Value.ToString());
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
                        if (layer.Name != ("Buffer Pool"))
                        {
                            layer.Transparent = false;
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
                if (this.keysDataGridView.SelectedRows.Count > 0)
                {
                    this.keysDataGridView.ClearSelection();
                }
            }

            keyChanging = false;
        }

        private void AllocationContainer_PageClicked(object sender, PageEventArgs e)
        {
            this.OnViewPage(sender, e);
        }

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

        private void ShowKeyToolStripButton_Click(object sender, EventArgs e)
        {
            splitContainer.Panel2Collapsed = !showKeyToolStripButton.Checked;
        }

        private void FileDetailsToolStripButton_CheckedChanged(object sender, EventArgs e)
        {
            if (this.fileDetailsToolStripButton.Enabled)
            {
                allocationContainer.ShowFileInformation = this.fileDetailsToolStripButton.Checked;
            }
        }
    }
}
