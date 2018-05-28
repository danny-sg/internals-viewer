using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI.Allocations
{
    /// <summary>
    /// Allocation Container containing one of more allocation maps
    /// </summary>
    public partial class AllocationContainer : UserControl
    {
        private Size extentSize = new Size(64, 8);
        private MapMode mode;
        private bool showFileInformation;

        public event EventHandler<PageEventArgs> PageClicked;
        public event EventHandler<PageEventArgs> PageOver;
        public event EventHandler RangeSelected;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllocationContainer"/> class.
        /// </summary>
        public AllocationContainer()
        {
            this.InitializeComponent();

            this.Paint += this.AllocationContainer_Paint;

            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);
        }

        /// <summary>
        /// Creates the allocation maps.
        /// </summary>
        /// <param name="files">The database files.</param>
        public void CreateAllocationMaps(List<DatabaseFile> files)
        {
            SuspendLayout();
            
            this.tableLayoutPanel.SuspendLayout();
            this.tableLayoutPanel.Controls.Clear();
            this.AllocationMaps.Clear();

            this.tableLayoutPanel.RowCount = 2;
            this.tableLayoutPanel.RowStyles.Clear();

            this.tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 1.0F));

            var fileIndex = 0;

            foreach (var file in files)
            {
                var allocationMap = this.CreateAllocationMap(file);
                allocationMap.ExtentSize = this.ExtentSize;
                allocationMap.Mode = this.Mode;

                var filePanel = new Panel();
                filePanel.Margin = new Padding(0);
                filePanel.Controls.Add(allocationMap);

                filePanel.Dock = DockStyle.Fill;
                this.tableLayoutPanel.Controls.Add(filePanel, 1, fileIndex);

                filePanel.Margin = new Padding(0, 0, 0, 4);

                if (fileIndex > 0)
                {
                    this.tableLayoutPanel.RowCount += 1;
                    this.tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 1.0F));
                }

                if (!this.showFileInformation && fileIndex == files.Count - 1)
                {
                    filePanel.Margin = new Padding(0);
                }
                else if(this.showFileInformation)
                {
                    filePanel.Controls.Add(new FileInformationControl(file));
                }

                fileIndex++;
            }

            this.tableLayoutPanel.Invalidate();
            this.tableLayoutPanel.ResumeLayout();

            ResumeLayout();
        }


        internal void CreateAllocationMaps(Dictionary<int, AllocationMap> dictionary)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Forces the control to invalidate its client area and immediately redraw itself and any child controls.
        /// </summary>
        /// <PermissionSet>
        /// 	<IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/>
        /// 	<IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/>
        /// 	<IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/>
        /// 	<IPermission class="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/>
        /// </PermissionSet>
        public override void Refresh()
        {
            base.Refresh();
            Invalidate();
        }

        /// <summary>
        /// Adds a map layer.
        /// </summary>
        /// <param name="layer">The layer.</param>
        public void AddMapLayer(AllocationLayer layer)
        {
            this.AllocationLayers.Add(layer);

            foreach (var allocationMap in this.AllocationMaps.Values)
            {
                allocationMap.Invalidate();
            }
        }

        /// <summary>
        /// Clears all map layers.
        /// </summary>
        public void ClearMapLayers()
        {
            this.AllocationLayers.Clear();

            foreach (var allocationMap in this.AllocationMaps.Values)
            {
                allocationMap.Invalidate();
            }
        }

        /// <summary>
        /// Removes a layer.
        /// </summary>
        /// <param name="name">The name of the layer to remove</param>
        /// <returns></returns>
        public bool RemoveLayer(string name)
        {
            var existing = this.AllocationLayers.Find(delegate(AllocationLayer layer) { return (layer.Name == name); });

            if (existing != null)
            {
                this.AllocationLayers.Remove(existing);

                foreach (var allocationMap in this.AllocationMaps.Values)
                {
                    allocationMap.Invalidate();
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Calculates the size of the fit.
        /// </summary>
        /// <returns></returns>
        public Size CalculateFitSize()
        {
            double maxExtentCount = 0;

            foreach (var map in this.AllocationMaps.Values)
            {
                if (map.ExtentCount > maxExtentCount)
                {
                    maxExtentCount = map.ExtentCount;
                }
            }

            double width = this.Width;
            double height = this.Height / 8;

            var extentsPerRow = Math.Sqrt(maxExtentCount / 8);

            var returnSize = new Size((int)(width / extentsPerRow), (int)(height / extentsPerRow));

            if (returnSize.Height < 1 || returnSize.Width < 1)
            {
                returnSize = new Size(8, 1);
            }

            return returnSize;
        }

        internal void ShowFittedMap()
        {
            foreach (var allocationMap in this.AllocationMaps.Values)
            {
                allocationMap.ShowFullMap();
            }
        }

        /// <summary>
        /// Handles the Paint event of the AllocationContainer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
        protected void AllocationContainer_Paint(object sender, PaintEventArgs e)
        {
            if (this.AllocationMaps.Count == 0)
            {
                ControlPaint.DrawBorder(e.Graphics,
                                        new Rectangle(0, 0, Width, Height),
                                        SystemColors.ControlDark,
                                        ButtonBorderStyle.Solid);
            }
        }

        /// <summary>
        /// Creates the allocation map.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        private AllocationMap CreateAllocationMap(DatabaseFile file)
        {
            var allocationMap = new AllocationMap();

            allocationMap.FileId = file.FileId;
            allocationMap.File = file;
            allocationMap.ExtentCount = file.Size / 8;
            allocationMap.Dock = DockStyle.Fill;
            allocationMap.MapLayers = this.AllocationLayers;

            allocationMap.PageClicked += this.AllocationMap_PageClicked;
            allocationMap.PageOver += this.AllocationMap_PageOver;
            allocationMap.RangeSelected += this.AllocationMap_RangeSelected;
            
            this.AllocationMaps.Add(file.FileId, allocationMap);

            return allocationMap;
        }

        /// <summary>
        /// Handles the RangeSelected event of the AllocationMap control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void AllocationMap_RangeSelected(object sender, EventArgs e)
        {
            var temp = this.RangeSelected;

            if (temp != null)
            {
                temp(this, e);
            }
        }

        /// <summary>
        /// Handles the PageOver event of the AllocationMap control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SqlInternals.AllocationInfo.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        private void AllocationMap_PageOver(object sender, PageEventArgs e)
        {
            var temp = this.PageOver;

            if (temp != null)
            {
                temp(this, e);
            }
        }

        /// <summary>
        /// Handles the PageClicked event of the AllocationMap control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SqlInternals.AllocationInfo.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        private void AllocationMap_PageClicked(object sender, PageEventArgs e)
        {
            var temp = this.PageClicked;

            if (temp != null)
            {
                temp(this, e);
            }
        }

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether [show file information].
        /// </summary>
        /// <value><c>true</c> if [show file information]; otherwise, <c>false</c>.</value>
        public bool ShowFileInformation
        {
            get
            {
                return this.showFileInformation;
            }

            set
            {
                if (value != this.showFileInformation)
                {
                    this.showFileInformation = value;
                    this.CreateAllocationMaps(InternalsViewerConnection.CurrentConnection().CurrentDatabase.Files);
                }
            }
        }

        /// <summary>
        /// Gets or sets the allocation map mode.
        /// </summary>
        /// <value>The mode.</value>
        public MapMode Mode
        {
            get
            {
                return this.mode;
            }

            set
            {
                this.mode = value;

                foreach (var allocationMap in this.AllocationMaps.Values)
                {
                    allocationMap.Mode = this.mode;
                }
            }
        }

        internal Dictionary<int, Pfs> Pfs
        {
            set
            {
                foreach (var allocationMap in AllocationMaps.Values)
                {
                    allocationMap.Pfs = value[allocationMap.FileId];
                }
            }
        }

        internal PfsByte PagePfsByte(PageAddress pageAddress)
        {
            return AllocationMaps[pageAddress.FileId].Pfs.PagePfsByte(pageAddress.PageId);
        }

        /// <summary>
        /// Gets the map layers collection
        /// </summary>
        /// <value>The map layers.</value>
        public List<AllocationLayer> AllocationLayers { get; } = new List<AllocationLayer>();

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

                foreach (var allocationMap in this.AllocationMaps.Values)
                {
                    allocationMap.ExtentSize = this.extentSize;
                }
            }
        }

        /// <summary>
        /// Gets or sets the layout style.
        /// </summary>
        /// <value>The layout style.</value>
        public LayoutStyle LayoutStyle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the IAM is included
        /// </summary>
        /// <value><c>true</c> if [include iam]; otherwise, <c>false</c>.</value>
        public bool IncludeIam { get; set; }

        /// <summary>
        /// Gets the allocation map Dictionary collection
        /// </summary>
        /// <value>The allocation maps.</value>
        public Dictionary<int, AllocationMap> AllocationMaps { get; } = new Dictionary<int, AllocationMap>();

        public bool DrawBorder
        {
            get
            {
                if (this.AllocationMaps.Count > 1)
                {
                    return this.AllocationMaps[0].DrawBorder;
                }
                else
                {
                    return true;
                }
            }

            set
            {
                foreach (var allocationMap in this.AllocationMaps.Values)
                {
                    allocationMap.DrawBorder = value;
                }
            }
        }

        public bool Holding
        {
            get
            {
                if (this.AllocationMaps.Count > 1)
                {
                    return this.AllocationMaps[InternalsViewerConnection.CurrentConnection().CurrentDatabase.Files[0].FileId].Holding;
                }
                else
                {
                    return true;
                }
            }

            set
            {
                foreach (var allocationMap in this.AllocationMaps.Values)
                {
                    allocationMap.Holding = value;
                }
            }
        }

        public string HoldingMessage
        {
            get
            {
                if (this.AllocationMaps.Count > 1)
                {
                    return this.AllocationMaps[0].HoldingMessage;
                }
                else
                {
                    return string.Empty;
                }
            }

            set
            {
                foreach (var allocationMap in this.AllocationMaps.Values)
                {
                    allocationMap.HoldingMessage = value;
                }
            }
        }

        #endregion

    }
}
