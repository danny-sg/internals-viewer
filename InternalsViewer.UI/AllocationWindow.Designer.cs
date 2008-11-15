using InternalsViewer.UI.Controls;
namespace InternalsViewer.UI
{
    partial class AllocationWindow
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.allocationContainer = new InternalsViewer.UI.Allocations.AllocationContainer();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.flatMenuStrip = new InternalsViewer.UI.Controls.FlatMenuStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.databaseToolStripComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.connectToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.pfsToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.bufferPoolToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.extentSizeToolStripComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.pageToolStripTextBox = new InternalsViewer.UI.Controls.PageAddressTextBox();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.showKeyToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.keysDataGridView = new System.Windows.Forms.DataGridView();
            this.KeyColumn = new InternalsViewer.UI.Controls.KeyImageColumn();
            this.NameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.IndexNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.IndexTypeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TotalPagesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UsedPagesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.allocationBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.errorImageToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.errorToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.allocUnitProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.allocUnitToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.spacerToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.AllocUnitLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.pageAddressToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.iconToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.allocUnitBackgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.keyImageColumn1 = new InternalsViewer.UI.Controls.KeyImageColumn();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.flatMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.keysDataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.allocationBindingSource)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // allocationContainer
            // 
            this.allocationContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.allocationContainer.DrawBorder = true;
            this.allocationContainer.ExtentSize = new System.Drawing.Size(64, 8);
            this.allocationContainer.Holding = true;
            this.allocationContainer.HoldingMessage = "";
            this.allocationContainer.IncludeIam = false;
            this.allocationContainer.LayoutStyle = InternalsViewer.UI.Allocations.LayoutStyle.Horizontal;
            this.allocationContainer.Location = new System.Drawing.Point(0, 30);
            this.allocationContainer.Mode = InternalsViewer.UI.Allocations.MapMode.Standard;
            this.allocationContainer.Name = "allocationContainer";
            this.allocationContainer.ShowFileInformation = false;
            this.allocationContainer.Size = new System.Drawing.Size(945, 422);
            this.allocationContainer.TabIndex = 2;
            this.allocationContainer.PageOver += new System.EventHandler<InternalsViewer.Internals.Pages.PageEventArgs>(this.AllocationContainer_PageOver);
            this.allocationContainer.PageClicked += new System.EventHandler<InternalsViewer.Internals.Pages.PageEventArgs>(this.AllocationContainer_PageClicked);
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.allocationContainer);
            this.splitContainer.Panel1.Controls.Add(this.flatMenuStrip);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.keysDataGridView);
            this.splitContainer.Panel2.Controls.Add(this.statusStrip);
            this.splitContainer.Size = new System.Drawing.Size(945, 579);
            this.splitContainer.SplitterDistance = 452;
            this.splitContainer.TabIndex = 0;
            // 
            // flatMenuStrip
            // 
            this.flatMenuStrip.AutoSize = false;
            this.flatMenuStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.flatMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.databaseToolStripComboBox,
            this.connectToolStripButton,
            this.toolStripSeparator1,
            this.pfsToolStripButton,
            this.bufferPoolToolStripButton,
            this.toolStripSeparator2,
            this.extentSizeToolStripComboBox,
            this.pageToolStripTextBox,
            this.toolStripLabel2,
            this.toolStripSeparator3,
            this.showKeyToolStripButton});
            this.flatMenuStrip.Location = new System.Drawing.Point(0, 0);
            this.flatMenuStrip.Name = "flatMenuStrip";
            this.flatMenuStrip.Padding = new System.Windows.Forms.Padding(4, 0, 1, 0);
            this.flatMenuStrip.Size = new System.Drawing.Size(945, 30);
            this.flatMenuStrip.TabIndex = 1;
            this.flatMenuStrip.Text = "flatMenuStrip1";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(53, 27);
            this.toolStripLabel1.Text = "Database";
            // 
            // databaseToolStripComboBox
            // 
            this.databaseToolStripComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.databaseToolStripComboBox.Enabled = false;
            this.databaseToolStripComboBox.Name = "databaseToolStripComboBox";
            this.databaseToolStripComboBox.Size = new System.Drawing.Size(121, 30);
            this.databaseToolStripComboBox.SelectedIndexChanged += new System.EventHandler(this.DatabaseToolStripComboBox_SelectedIndexChanged);
            // 
            // connectToolStripButton
            // 
            this.connectToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.connectToolStripButton.Image = global::InternalsViewer.UI.Properties.Resources.connect;
            this.connectToolStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.connectToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.connectToolStripButton.Name = "connectToolStripButton";
            this.connectToolStripButton.Size = new System.Drawing.Size(23, 27);
            this.connectToolStripButton.Text = "Connect to database";
            this.connectToolStripButton.Click += new System.EventHandler(this.OnConnect);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 30);
            // 
            // pfsToolStripButton
            // 
            this.pfsToolStripButton.CheckOnClick = true;
            this.pfsToolStripButton.Enabled = false;
            this.pfsToolStripButton.Image = global::InternalsViewer.UI.Properties.Resources.pfs;
            this.pfsToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.pfsToolStripButton.Name = "pfsToolStripButton";
            this.pfsToolStripButton.Size = new System.Drawing.Size(45, 27);
            this.pfsToolStripButton.Text = "PFS";
            this.pfsToolStripButton.Click += new System.EventHandler(this.PfsToolStripButton_Click);
            // 
            // bufferPoolToolStripButton
            // 
            this.bufferPoolToolStripButton.CheckOnClick = true;
            this.bufferPoolToolStripButton.Enabled = false;
            this.bufferPoolToolStripButton.Image = global::InternalsViewer.UI.Properties.Resources.bufferpool;
            this.bufferPoolToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bufferPoolToolStripButton.Name = "bufferPoolToolStripButton";
            this.bufferPoolToolStripButton.Size = new System.Drawing.Size(80, 27);
            this.bufferPoolToolStripButton.Text = "Buffer Pool";
            this.bufferPoolToolStripButton.Click += new System.EventHandler(this.BufferPoolToolStripButton_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 30);
            // 
            // extentSizeToolStripComboBox
            // 
            this.extentSizeToolStripComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.extentSizeToolStripComboBox.Enabled = false;
            this.extentSizeToolStripComboBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.extentSizeToolStripComboBox.Items.AddRange(new object[] {
            "Small",
            "Medium",
            "Large",
            "Fit"});
            this.extentSizeToolStripComboBox.Name = "extentSizeToolStripComboBox";
            this.extentSizeToolStripComboBox.Size = new System.Drawing.Size(100, 30);
            this.extentSizeToolStripComboBox.SelectedIndexChanged += new System.EventHandler(this.ExtentSizeToolStripComboBox_SelectedIndexChanged);
            // 
            // pageToolStripTextBox
            // 
            this.pageToolStripTextBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.pageToolStripTextBox.AutoSize = false;
            this.pageToolStripTextBox.DatabaseId = 0;
            this.pageToolStripTextBox.ForeColor = System.Drawing.SystemColors.InactiveCaption;
            this.pageToolStripTextBox.Name = "pageToolStripTextBox";
            this.pageToolStripTextBox.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.pageToolStripTextBox.Size = new System.Drawing.Size(90, 28);
            this.pageToolStripTextBox.Text = "(File Id:Page Id)";
            this.pageToolStripTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PageToolStripTextBox_KeyDown);
            // 
            // toolStripLabel2
            // 
            this.toolStripLabel2.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripLabel2.Name = "toolStripLabel2";
            this.toolStripLabel2.Size = new System.Drawing.Size(35, 27);
            this.toolStripLabel2.Text = "Page:";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 30);
            // 
            // showKeyToolStripButton
            // 
            this.showKeyToolStripButton.Checked = true;
            this.showKeyToolStripButton.CheckOnClick = true;
            this.showKeyToolStripButton.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showKeyToolStripButton.Image = global::InternalsViewer.UI.Properties.Resources.WindowSplit;
            this.showKeyToolStripButton.ImageTransparentColor = System.Drawing.Color.Lime;
            this.showKeyToolStripButton.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.showKeyToolStripButton.Name = "showKeyToolStripButton";
            this.showKeyToolStripButton.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.showKeyToolStripButton.Size = new System.Drawing.Size(45, 26);
            this.showKeyToolStripButton.Text = "Key";
            this.showKeyToolStripButton.Click += new System.EventHandler(this.ShowKeyToolStripButton_Click);
            // 
            // keysDataGridView
            // 
            this.keysDataGridView.AllowUserToAddRows = false;
            this.keysDataGridView.AllowUserToDeleteRows = false;
            this.keysDataGridView.AllowUserToOrderColumns = true;
            this.keysDataGridView.AllowUserToResizeColumns = false;
            this.keysDataGridView.AllowUserToResizeRows = false;
            this.keysDataGridView.AutoGenerateColumns = false;
            this.keysDataGridView.BackgroundColor = System.Drawing.Color.White;
            this.keysDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.keysDataGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.keysDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.keysDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.keysDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.KeyColumn,
            this.NameColumn,
            this.IndexNameColumn,
            this.IndexTypeColumn,
            this.TotalPagesColumn,
            this.UsedPagesColumn});
            this.keysDataGridView.DataSource = this.allocationBindingSource;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.ControlLight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.keysDataGridView.DefaultCellStyle = dataGridViewCellStyle3;
            this.keysDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.keysDataGridView.GridColor = System.Drawing.Color.White;
            this.keysDataGridView.Location = new System.Drawing.Point(0, 0);
            this.keysDataGridView.Name = "keysDataGridView";
            this.keysDataGridView.ReadOnly = true;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.keysDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.keysDataGridView.RowHeadersVisible = false;
            this.keysDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.keysDataGridView.Size = new System.Drawing.Size(945, 101);
            this.keysDataGridView.TabIndex = 2;
            this.keysDataGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.KeysDataGridView_CellClick);
            this.keysDataGridView.SelectionChanged += new System.EventHandler(this.KeysDataGridView_SelectionChanged);
            // 
            // KeyColumn
            // 
            this.KeyColumn.DataPropertyName = "Colour";
            this.KeyColumn.HeaderText = "";
            this.KeyColumn.Name = "KeyColumn";
            this.KeyColumn.ReadOnly = true;
            this.KeyColumn.Width = 30;
            // 
            // NameColumn
            // 
            this.NameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.NameColumn.DataPropertyName = "ObjectName";
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.WhiteSmoke;
            this.NameColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.NameColumn.HeaderText = "Table";
            this.NameColumn.Name = "NameColumn";
            this.NameColumn.ReadOnly = true;
            // 
            // IndexNameColumn
            // 
            this.IndexNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.IndexNameColumn.DataPropertyName = "IndexName";
            this.IndexNameColumn.HeaderText = "Index";
            this.IndexNameColumn.Name = "IndexNameColumn";
            this.IndexNameColumn.ReadOnly = true;
            // 
            // IndexTypeColumn
            // 
            this.IndexTypeColumn.DataPropertyName = "IndexType";
            this.IndexTypeColumn.HeaderText = "Index Type";
            this.IndexTypeColumn.Name = "IndexTypeColumn";
            this.IndexTypeColumn.ReadOnly = true;
            // 
            // TotalPagesColumn
            // 
            this.TotalPagesColumn.DataPropertyName = "TotalPages";
            this.TotalPagesColumn.HeaderText = "Total Pages";
            this.TotalPagesColumn.Name = "TotalPagesColumn";
            this.TotalPagesColumn.ReadOnly = true;
            this.TotalPagesColumn.Width = 90;
            // 
            // UsedPagesColumn
            // 
            this.UsedPagesColumn.DataPropertyName = "UsedPages";
            this.UsedPagesColumn.HeaderText = "Used Pages";
            this.UsedPagesColumn.Name = "UsedPagesColumn";
            this.UsedPagesColumn.ReadOnly = true;
            this.UsedPagesColumn.Width = 90;
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.errorImageToolStripStatusLabel,
            this.errorToolStripStatusLabel,
            this.allocUnitProgressBar,
            this.allocUnitToolStripStatusLabel,
            this.spacerToolStripStatusLabel,
            this.toolStripStatusLabel2,
            this.AllocUnitLabel,
            this.pageAddressToolStripStatusLabel,
            this.iconToolStripStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 101);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(945, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 1;
            this.statusStrip.Text = "statusStrip1";
            // 
            // errorImageToolStripStatusLabel
            // 
            this.errorImageToolStripStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.errorImageToolStripStatusLabel.Name = "errorImageToolStripStatusLabel";
            this.errorImageToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            this.errorImageToolStripStatusLabel.Text = "iii";
            // 
            // errorToolStripStatusLabel
            // 
            this.errorToolStripStatusLabel.Name = "errorToolStripStatusLabel";
            this.errorToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // allocUnitProgressBar
            // 
            this.allocUnitProgressBar.Enabled = false;
            this.allocUnitProgressBar.Name = "allocUnitProgressBar";
            this.allocUnitProgressBar.Size = new System.Drawing.Size(100, 16);
            this.allocUnitProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.allocUnitProgressBar.Visible = false;
            // 
            // allocUnitToolStripStatusLabel
            // 
            this.allocUnitToolStripStatusLabel.Name = "allocUnitToolStripStatusLabel";
            this.allocUnitToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // spacerToolStripStatusLabel
            // 
            this.spacerToolStripStatusLabel.Name = "spacerToolStripStatusLabel";
            this.spacerToolStripStatusLabel.Size = new System.Drawing.Size(930, 17);
            this.spacerToolStripStatusLabel.Spring = true;
            // 
            // toolStripStatusLabel2
            // 
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new System.Drawing.Size(0, 17);
            // 
            // AllocUnitLabel
            // 
            this.AllocUnitLabel.Name = "AllocUnitLabel";
            this.AllocUnitLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // pageAddressToolStripStatusLabel
            // 
            this.pageAddressToolStripStatusLabel.ForeColor = System.Drawing.Color.Navy;
            this.pageAddressToolStripStatusLabel.Name = "pageAddressToolStripStatusLabel";
            this.pageAddressToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // iconToolStripStatusLabel
            // 
            this.iconToolStripStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.iconToolStripStatusLabel.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.iconToolStripStatusLabel.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.iconToolStripStatusLabel.Name = "iconToolStripStatusLabel";
            this.iconToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            this.iconToolStripStatusLabel.Text = "iconToolStripStatusLabel";
            // 
            // allocUnitBackgroundWorker
            // 
            this.allocUnitBackgroundWorker.WorkerReportsProgress = true;
            this.allocUnitBackgroundWorker.WorkerSupportsCancellation = true;
            this.allocUnitBackgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.AllocUnitBackgroundWorker_DoWork);
            this.allocUnitBackgroundWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.AllocUnitBackgroundWorker_RunWorkerCompleted);
            this.allocUnitBackgroundWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.AllocUnitBackgroundWorker_ProgressChanged);
            // 
            // keyImageColumn1
            // 
            this.keyImageColumn1.DataPropertyName = "Colour";
            this.keyImageColumn1.HeaderText = "";
            this.keyImageColumn1.Name = "keyImageColumn1";
            this.keyImageColumn1.Width = 30;
            // 
            // AllocationWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.Name = "AllocationWindow";
            this.Size = new System.Drawing.Size(945, 579);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            this.splitContainer.ResumeLayout(false);
            this.flatMenuStrip.ResumeLayout(false);
            this.flatMenuStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.keysDataGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.allocationBindingSource)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private InternalsViewer.UI.Allocations.AllocationContainer allocationContainer;
        private FlatMenuStrip flatMenuStrip;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripComboBox databaseToolStripComboBox;
        private System.Windows.Forms.ToolStripButton connectToolStripButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.ComponentModel.BackgroundWorker allocUnitBackgroundWorker;
        private System.Windows.Forms.ToolStripComboBox extentSizeToolStripComboBox;
        private System.Windows.Forms.ToolStripButton pfsToolStripButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton bufferPoolToolStripButton;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel errorImageToolStripStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel errorToolStripStatusLabel;
        private System.Windows.Forms.ToolStripProgressBar allocUnitProgressBar;
        private System.Windows.Forms.ToolStripStatusLabel allocUnitToolStripStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel spacerToolStripStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel2;
        private System.Windows.Forms.ToolStripStatusLabel AllocUnitLabel;
        private System.Windows.Forms.ToolStripStatusLabel pageAddressToolStripStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel iconToolStripStatusLabel;
        private System.Windows.Forms.BindingSource allocationBindingSource;
        private System.Windows.Forms.DataGridView keysDataGridView;
        private KeyImageColumn keyImageColumn1;
        private KeyImageColumn KeyColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn NameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn IndexNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn IndexTypeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn TotalPagesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn UsedPagesColumn;
        private PageAddressTextBox pageToolStripTextBox;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton showKeyToolStripButton;
    }
}
