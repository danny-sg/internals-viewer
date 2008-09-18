using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.UI.Allocations;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI
{
    public partial class AllocationWindow : UserControl
    {
        public event EventHandler Connect;
        protected delegate void LoadDatabaseDelegate();
        private BufferPool bufferPool = new BufferPool();

        public AllocationWindow()
        {
            InitializeComponent();

            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);

            extentSizeToolStripComboBox.SelectedIndex = 0;
        }

        internal virtual void OnConnect(object sender, EventArgs e)
        {
            if (this.Connect != null)
            {
                this.Connect(sender, e);
            }
        }

        private void EnableToolbar(bool enabled)
        {
            databaseToolStripComboBox.Enabled = enabled;
            extentSizeToolStripComboBox.Enabled = enabled;
            bufferPoolToolStripButton.Enabled = enabled;
            pfsToolStripButton.Enabled = enabled;
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

            allocationContainer.Holding = true;
            allocationContainer.HoldingMessage = "Scanning allocations...";

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
            e.Result = AllocationUnitsLayer.GenerateLayers(InternalsViewerConnection.CurrentConnection().CurrentDatabase, (BackgroundWorker)sender);
        }

        private void AllocUnitBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

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

            // this.ChangeRowKeyColours(layers);

            if (this.bufferPoolToolStripButton.Checked)
            {
                this.ShowBufferPool(true);
            }

            if (this.allocationContainer.Mode == MapMode.Full)
            {
                this.allocationContainer.ShowFittedMap();
            }
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

                if (allocationContainer.MapLayers.Count == 0)
                {
                    DisplayLayers();
                }
            }

            allocationContainer.Refresh();
        }

        private void databaseToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            InternalsViewerConnection.CurrentConnection().CurrentDatabase = (Database)databaseToolStripComboBox.SelectedItem;

            this.LoadDatabase();
        }

        private void extentSizeToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
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
                            case 2:
                                AllocUnitLabel.Text = "GAM";
                                break;
                            case 3:
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

                    List<string> layers = AllocationLayer.FindPage(e.Address, allocationContainer.MapLayers);

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

        private void allocationContainer_PageOver(object sender, PageEventArgs e)
        {

        }
    }
}
