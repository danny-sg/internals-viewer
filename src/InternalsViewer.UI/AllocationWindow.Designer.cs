﻿using System.ComponentModel;
using System.Windows.Forms;
using InternalsViewer.UI.Allocations;
using InternalsViewer.UI.Controls;

namespace InternalsViewer.UI
{
    partial class AllocationWindow
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

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
            components = new Container();
            var dataGridViewCellStyle8 = new DataGridViewCellStyle();
            var dataGridViewCellStyle13 = new DataGridViewCellStyle();
            var dataGridViewCellStyle14 = new DataGridViewCellStyle();
            var dataGridViewCellStyle9 = new DataGridViewCellStyle();
            var dataGridViewCellStyle10 = new DataGridViewCellStyle();
            var dataGridViewCellStyle11 = new DataGridViewCellStyle();
            var dataGridViewCellStyle12 = new DataGridViewCellStyle();
            allocationContainer = new AllocationContainer();
            splitContainer = new SplitContainer();
            flatMenuStrip = new FlatMenuStrip();
            toolStripLabel1 = new ToolStripLabel();
            databaseToolStripComboBox = new ToolStripComboBox();
            connectToolStripButton = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            mapToolStripButton = new ToolStripSplitButton();
            allocationUnitsToolStripMenuItem = new ToolStripMenuItem();
            allocationMapsToolStripMenuItem = new ToolStripMenuItem();
            gamToolStripMenuItem = new ToolStripMenuItem();
            sgamToolStripMenuItem = new ToolStripMenuItem();
            bcmToolStripMenuItem = new ToolStripMenuItem();
            dcmToolStripMenuItem = new ToolStripMenuItem();
            pFSToolStripMenuItem = new ToolStripMenuItem();
            bufferPoolToolStripButton = new ToolStripButton();
            showKeyToolStripButton = new ToolStripButton();
            fileDetailsToolStripButton = new ToolStripButton();
            toolStripSeparator2 = new ToolStripSeparator();
            extentSizeToolStripComboBox = new ToolStripComboBox();
            pageToolStripTextBox = new PageAddressTextBox();
            toolStripLabel2 = new ToolStripLabel();
            keysDataGridView = new DataGridView();
            KeyColumn = new KeyImageColumn();
            NameColumn = new DataGridViewTextBoxColumn();
            IndexNameColumn = new DataGridViewTextBoxColumn();
            IndexTypeColumn = new DataGridViewTextBoxColumn();
            RootPageColumn = new DataGridViewTextBoxColumn();
            FirstPageColumn = new DataGridViewTextBoxColumn();
            FirstIamPageColumn = new DataGridViewTextBoxColumn();
            TotalPagesColumn = new DataGridViewTextBoxColumn();
            AllocationBindingSource = new BindingSource(components);
            statusStrip = new StatusStrip();
            errorImageToolStripStatusLabel = new ToolStripStatusLabel();
            errorToolStripStatusLabel = new ToolStripStatusLabel();
            allocUnitProgressBar = new ToolStripProgressBar();
            allocUnitToolStripStatusLabel = new ToolStripStatusLabel();
            spacerToolStripStatusLabel = new ToolStripStatusLabel();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            AllocUnitLabel = new ToolStripStatusLabel();
            pageAddressToolStripStatusLabel = new ToolStripStatusLabel();
            iconToolStripStatusLabel = new ToolStripStatusLabel();
            allocUnitBackgroundWorker = new BackgroundWorker();
            keyImageColumn1 = new KeyImageColumn();
            ((ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            flatMenuStrip.SuspendLayout();
            ((ISupportInitialize)keysDataGridView).BeginInit();
            ((ISupportInitialize)AllocationBindingSource).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // allocationContainer
            // 
            allocationContainer.CurrentDatabase = null;
            allocationContainer.Dock = DockStyle.Fill;
            allocationContainer.DrawBorder = true;
            allocationContainer.ExtentSize = new System.Drawing.Size(64, 8);
            allocationContainer.Holding = true;
            allocationContainer.HoldingMessage = "";
            allocationContainer.IncludeIam = false;
            allocationContainer.LayoutStyle = LayoutStyle.Horizontal;
            allocationContainer.Location = new System.Drawing.Point(0, 35);
            allocationContainer.Margin = new Padding(7);
            allocationContainer.Mode = MapMode.Standard;
            allocationContainer.Name = "allocationContainer";
            allocationContainer.ShowFileInformation = false;
            allocationContainer.Size = new System.Drawing.Size(1102, 467);
            allocationContainer.TabIndex = 2;
            allocationContainer.PageClicked += AllocationContainer_PageClicked;
            allocationContainer.PageOver += AllocationContainer_PageOver;
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new System.Drawing.Point(0, 0);
            splitContainer.Margin = new Padding(4);
            splitContainer.Name = "splitContainer";
            splitContainer.Orientation = Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(allocationContainer);
            splitContainer.Panel1.Controls.Add(flatMenuStrip);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(keysDataGridView);
            splitContainer.Size = new System.Drawing.Size(1102, 646);
            splitContainer.SplitterDistance = 502;
            splitContainer.SplitterWidth = 5;
            splitContainer.TabIndex = 0;
            // 
            // flatMenuStrip
            // 
            flatMenuStrip.AutoSize = false;
            flatMenuStrip.BackColor = System.Drawing.SystemColors.Control;
            flatMenuStrip.GripStyle = ToolStripGripStyle.Hidden;
            flatMenuStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            flatMenuStrip.Items.AddRange(new ToolStripItem[] { toolStripLabel1, databaseToolStripComboBox, connectToolStripButton, toolStripSeparator1, mapToolStripButton, bufferPoolToolStripButton, showKeyToolStripButton, fileDetailsToolStripButton, toolStripSeparator2, extentSizeToolStripComboBox, pageToolStripTextBox, toolStripLabel2 });
            flatMenuStrip.Location = new System.Drawing.Point(0, 0);
            flatMenuStrip.Name = "flatMenuStrip";
            flatMenuStrip.Padding = new Padding(5, 0, 1, 0);
            flatMenuStrip.Size = new System.Drawing.Size(1102, 35);
            flatMenuStrip.TabIndex = 1;
            flatMenuStrip.Text = "flatMenuStrip1";
            // 
            // toolStripLabel1
            // 
            toolStripLabel1.Name = "toolStripLabel1";
            toolStripLabel1.Size = new System.Drawing.Size(55, 32);
            toolStripLabel1.Text = "Database";
            // 
            // databaseToolStripComboBox
            // 
            databaseToolStripComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            databaseToolStripComboBox.Enabled = false;
            databaseToolStripComboBox.Name = "databaseToolStripComboBox";
            databaseToolStripComboBox.Size = new System.Drawing.Size(140, 35);
            databaseToolStripComboBox.SelectedIndexChanged += DatabaseToolStripComboBox_SelectedIndexChanged;
            // 
            // connectToolStripButton
            // 
            connectToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            connectToolStripButton.Image = Properties.Resources.connect1;
            connectToolStripButton.ImageScaling = ToolStripItemImageScaling.None;
            connectToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            connectToolStripButton.Name = "connectToolStripButton";
            connectToolStripButton.Size = new System.Drawing.Size(23, 32);
            connectToolStripButton.Text = "Connect to database";
            connectToolStripButton.Click += OnConnect;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(6, 35);
            // 
            // mapToolStripButton
            // 
            mapToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            mapToolStripButton.DropDownItems.AddRange(new ToolStripItem[] { allocationUnitsToolStripMenuItem, allocationMapsToolStripMenuItem, pFSToolStripMenuItem });
            mapToolStripButton.Enabled = false;
            mapToolStripButton.Image = Properties.Resources.allocationMapIcon;
            mapToolStripButton.ImageScaling = ToolStripItemImageScaling.None;
            mapToolStripButton.ImageTransparentColor = System.Drawing.Color.Lime;
            mapToolStripButton.Name = "mapToolStripButton";
            mapToolStripButton.Size = new System.Drawing.Size(32, 32);
            mapToolStripButton.Text = "Allocation Units";
            // 
            // allocationUnitsToolStripMenuItem
            // 
            allocationUnitsToolStripMenuItem.Image = Properties.Resources.allocationMapIcon;
            allocationUnitsToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            allocationUnitsToolStripMenuItem.Name = "allocationUnitsToolStripMenuItem";
            allocationUnitsToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            allocationUnitsToolStripMenuItem.Text = "Allocation Units";
            allocationUnitsToolStripMenuItem.Click += AllocationUnitsToolStripMenuItem_Click;
            // 
            // allocationMapsToolStripMenuItem
            // 
            allocationMapsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { gamToolStripMenuItem, sgamToolStripMenuItem, bcmToolStripMenuItem, dcmToolStripMenuItem });
            allocationMapsToolStripMenuItem.Image = Properties.Resources.GAMallocationMapIcon;
            allocationMapsToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            allocationMapsToolStripMenuItem.Name = "allocationMapsToolStripMenuItem";
            allocationMapsToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            allocationMapsToolStripMenuItem.Text = "Allocation Maps";
            allocationMapsToolStripMenuItem.Click += AllocationMapsToolStripMenuItem_Click;
            // 
            // gamToolStripMenuItem
            // 
            gamToolStripMenuItem.Checked = true;
            gamToolStripMenuItem.CheckOnClick = true;
            gamToolStripMenuItem.CheckState = CheckState.Checked;
            gamToolStripMenuItem.Name = "gamToolStripMenuItem";
            gamToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            gamToolStripMenuItem.Text = "GAM";
            // 
            // sgamToolStripMenuItem
            // 
            sgamToolStripMenuItem.Checked = true;
            sgamToolStripMenuItem.CheckOnClick = true;
            sgamToolStripMenuItem.CheckState = CheckState.Checked;
            sgamToolStripMenuItem.Name = "sgamToolStripMenuItem";
            sgamToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            sgamToolStripMenuItem.Text = "SGAM";
            // 
            // bcmToolStripMenuItem
            // 
            bcmToolStripMenuItem.CheckOnClick = true;
            bcmToolStripMenuItem.Name = "bcmToolStripMenuItem";
            bcmToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            bcmToolStripMenuItem.Text = "BCM";
            // 
            // dcmToolStripMenuItem
            // 
            dcmToolStripMenuItem.CheckOnClick = true;
            dcmToolStripMenuItem.Name = "dcmToolStripMenuItem";
            dcmToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            dcmToolStripMenuItem.Text = "DCM";
            // 
            // pFSToolStripMenuItem
            // 
            pFSToolStripMenuItem.Image = Properties.Resources.pfs;
            pFSToolStripMenuItem.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            pFSToolStripMenuItem.ImageScaling = ToolStripItemImageScaling.None;
            pFSToolStripMenuItem.Name = "pFSToolStripMenuItem";
            pFSToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
            pFSToolStripMenuItem.Text = "PFS (Page Free Space)";
            pFSToolStripMenuItem.Click += pFSToolStripMenuItem_Click;
            // 
            // bufferPoolToolStripButton
            // 
            bufferPoolToolStripButton.CheckOnClick = true;
            bufferPoolToolStripButton.Enabled = false;
            bufferPoolToolStripButton.Image = Properties.Resources.bufferpool;
            bufferPoolToolStripButton.ImageScaling = ToolStripItemImageScaling.None;
            bufferPoolToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            bufferPoolToolStripButton.Name = "bufferPoolToolStripButton";
            bufferPoolToolStripButton.Size = new System.Drawing.Size(86, 32);
            bufferPoolToolStripButton.Text = "Buffer Pool";
            bufferPoolToolStripButton.Click += BufferPoolToolStripButton_Click;
            // 
            // showKeyToolStripButton
            // 
            showKeyToolStripButton.Checked = true;
            showKeyToolStripButton.CheckOnClick = true;
            showKeyToolStripButton.CheckState = CheckState.Checked;
            showKeyToolStripButton.Enabled = false;
            showKeyToolStripButton.Image = Properties.Resources.WindowSplit;
            showKeyToolStripButton.ImageScaling = ToolStripItemImageScaling.None;
            showKeyToolStripButton.ImageTransparentColor = System.Drawing.Color.Lime;
            showKeyToolStripButton.Margin = new Padding(0, 2, 0, 2);
            showKeyToolStripButton.Name = "showKeyToolStripButton";
            showKeyToolStripButton.Padding = new Padding(0, 2, 0, 2);
            showKeyToolStripButton.Size = new System.Drawing.Size(46, 31);
            showKeyToolStripButton.Text = "Key";
            showKeyToolStripButton.Click += ShowKeyToolStripButton_Click;
            // 
            // fileDetailsToolStripButton
            // 
            fileDetailsToolStripButton.CheckOnClick = true;
            fileDetailsToolStripButton.Enabled = false;
            fileDetailsToolStripButton.Image = Properties.Resources.fileinfo1;
            fileDetailsToolStripButton.ImageScaling = ToolStripItemImageScaling.None;
            fileDetailsToolStripButton.ImageTransparentColor = System.Drawing.Color.Lime;
            fileDetailsToolStripButton.Margin = new Padding(4, 2, 0, 2);
            fileDetailsToolStripButton.Name = "fileDetailsToolStripButton";
            fileDetailsToolStripButton.Padding = new Padding(4, 2, 0, 2);
            fileDetailsToolStripButton.Size = new System.Drawing.Size(87, 31);
            fileDetailsToolStripButton.Text = "File Details";
            fileDetailsToolStripButton.CheckedChanged += FileDetailsToolStripButton_CheckedChanged;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(6, 35);
            // 
            // extentSizeToolStripComboBox
            // 
            extentSizeToolStripComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            extentSizeToolStripComboBox.Enabled = false;
            extentSizeToolStripComboBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            extentSizeToolStripComboBox.Items.AddRange(new object[] { "Small", "Medium", "Large", "Fit" });
            extentSizeToolStripComboBox.Name = "extentSizeToolStripComboBox";
            extentSizeToolStripComboBox.Size = new System.Drawing.Size(116, 35);
            extentSizeToolStripComboBox.SelectedIndexChanged += ExtentSizeToolStripComboBox_SelectedIndexChanged;
            // 
            // pageToolStripTextBox
            // 
            pageToolStripTextBox.Alignment = ToolStripItemAlignment.Right;
            pageToolStripTextBox.AutoSize = false;
            pageToolStripTextBox.DatabaseId = 0;
            pageToolStripTextBox.ForeColor = System.Drawing.SystemColors.InactiveCaption;
            pageToolStripTextBox.Name = "pageToolStripTextBox";
            pageToolStripTextBox.Padding = new Padding(0, 0, 2, 0);
            pageToolStripTextBox.Size = new System.Drawing.Size(43, 23);
            pageToolStripTextBox.Text = "(File Id:Page Id)";
            pageToolStripTextBox.KeyDown += PageToolStripTextBox_KeyDown;
            // 
            // toolStripLabel2
            // 
            toolStripLabel2.Alignment = ToolStripItemAlignment.Right;
            toolStripLabel2.Name = "toolStripLabel2";
            toolStripLabel2.Size = new System.Drawing.Size(36, 32);
            toolStripLabel2.Text = "Page:";
            // 
            // keysDataGridView
            // 
            keysDataGridView.AllowUserToAddRows = false;
            keysDataGridView.AllowUserToDeleteRows = false;
            keysDataGridView.AllowUserToOrderColumns = true;
            keysDataGridView.AllowUserToResizeRows = false;
            keysDataGridView.AutoGenerateColumns = false;
            keysDataGridView.BackgroundColor = System.Drawing.Color.White;
            keysDataGridView.BorderStyle = BorderStyle.None;
            keysDataGridView.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dataGridViewCellStyle8.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = DataGridViewTriState.True;
            keysDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle8;
            keysDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            keysDataGridView.Columns.AddRange(new DataGridViewColumn[] { KeyColumn, NameColumn, IndexNameColumn, IndexTypeColumn, RootPageColumn, FirstPageColumn, FirstIamPageColumn, TotalPagesColumn });
            keysDataGridView.DataSource = AllocationBindingSource;
            dataGridViewCellStyle13.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle13.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle13.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            dataGridViewCellStyle13.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle13.SelectionBackColor = System.Drawing.SystemColors.ControlLight;
            dataGridViewCellStyle13.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle13.WrapMode = DataGridViewTriState.False;
            keysDataGridView.DefaultCellStyle = dataGridViewCellStyle13;
            keysDataGridView.Dock = DockStyle.Fill;
            keysDataGridView.GridColor = System.Drawing.Color.White;
            keysDataGridView.Location = new System.Drawing.Point(0, 0);
            keysDataGridView.Margin = new Padding(4);
            keysDataGridView.Name = "keysDataGridView";
            keysDataGridView.ReadOnly = true;
            dataGridViewCellStyle14.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle14.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle14.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            dataGridViewCellStyle14.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle14.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle14.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle14.WrapMode = DataGridViewTriState.True;
            keysDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle14;
            keysDataGridView.RowHeadersVisible = false;
            keysDataGridView.RowHeadersWidth = 102;
            keysDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            keysDataGridView.Size = new System.Drawing.Size(1102, 139);
            keysDataGridView.TabIndex = 2;
            keysDataGridView.CellClick += KeysDataGridView_CellClick;
            keysDataGridView.CellContentClick += keysDataGridView_CellContentClick;
            keysDataGridView.SelectionChanged += KeysDataGridView_SelectionChanged;
            // 
            // KeyColumn
            // 
            KeyColumn.DataPropertyName = "Colour";
            KeyColumn.HeaderText = "";
            KeyColumn.MinimumWidth = 12;
            KeyColumn.Name = "KeyColumn";
            KeyColumn.ReadOnly = true;
            KeyColumn.Width = 30;
            // 
            // NameColumn
            // 
            NameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            NameColumn.DataPropertyName = "ObjectName";
            dataGridViewCellStyle9.BackColor = System.Drawing.Color.WhiteSmoke;
            NameColumn.DefaultCellStyle = dataGridViewCellStyle9;
            NameColumn.HeaderText = "Table";
            NameColumn.MinimumWidth = 200;
            NameColumn.Name = "NameColumn";
            NameColumn.ReadOnly = true;
            // 
            // IndexNameColumn
            // 
            IndexNameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            IndexNameColumn.DataPropertyName = "IndexName";
            IndexNameColumn.HeaderText = "Index";
            IndexNameColumn.MinimumWidth = 200;
            IndexNameColumn.Name = "IndexNameColumn";
            IndexNameColumn.ReadOnly = true;
            // 
            // IndexTypeColumn
            // 
            IndexTypeColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            IndexTypeColumn.DataPropertyName = "IndexType";
            IndexTypeColumn.HeaderText = "Index Type";
            IndexTypeColumn.MinimumWidth = 12;
            IndexTypeColumn.Name = "IndexTypeColumn";
            IndexTypeColumn.ReadOnly = true;
            // 
            // RootPageColumn
            // 
            RootPageColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            RootPageColumn.DataPropertyName = "RootPage";
            dataGridViewCellStyle10.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle10.ForeColor = System.Drawing.Color.FromArgb(0, 0, 192);
            RootPageColumn.DefaultCellStyle = dataGridViewCellStyle10;
            RootPageColumn.HeaderText = "Root Page";
            RootPageColumn.MinimumWidth = 12;
            RootPageColumn.Name = "RootPageColumn";
            RootPageColumn.ReadOnly = true;
            // 
            // FirstPageColumn
            // 
            FirstPageColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            FirstPageColumn.DataPropertyName = "FirstPage";
            dataGridViewCellStyle11.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle11.ForeColor = System.Drawing.Color.FromArgb(0, 0, 192);
            FirstPageColumn.DefaultCellStyle = dataGridViewCellStyle11;
            FirstPageColumn.HeaderText = "First Page";
            FirstPageColumn.MinimumWidth = 12;
            FirstPageColumn.Name = "FirstPageColumn";
            FirstPageColumn.ReadOnly = true;
            // 
            // FirstIamPageColumn
            // 
            FirstIamPageColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            FirstIamPageColumn.DataPropertyName = "FirstIamPage";
            dataGridViewCellStyle12.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle12.ForeColor = System.Drawing.Color.FromArgb(0, 0, 192);
            FirstIamPageColumn.DefaultCellStyle = dataGridViewCellStyle12;
            FirstIamPageColumn.HeaderText = "First IAM";
            FirstIamPageColumn.MinimumWidth = 12;
            FirstIamPageColumn.Name = "FirstIamPageColumn";
            FirstIamPageColumn.ReadOnly = true;
            // 
            // TotalPagesColumn
            // 
            TotalPagesColumn.DataPropertyName = "TotalPages";
            TotalPagesColumn.HeaderText = "Total Pages";
            TotalPagesColumn.MinimumWidth = 12;
            TotalPagesColumn.Name = "TotalPagesColumn";
            TotalPagesColumn.ReadOnly = true;
            TotalPagesColumn.Width = 90;
            // 
            // AllocationBindingSource
            // 
            AllocationBindingSource.AllowNew = false;
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            statusStrip.Items.AddRange(new ToolStripItem[] { errorImageToolStripStatusLabel, errorToolStripStatusLabel, allocUnitProgressBar, allocUnitToolStripStatusLabel, spacerToolStripStatusLabel, toolStripStatusLabel2, AllocUnitLabel, pageAddressToolStripStatusLabel, iconToolStripStatusLabel });
            statusStrip.Location = new System.Drawing.Point(0, 646);
            statusStrip.Name = "statusStrip";
            statusStrip.Padding = new Padding(1, 0, 16, 0);
            statusStrip.Size = new System.Drawing.Size(1102, 22);
            statusStrip.SizingGrip = false;
            statusStrip.TabIndex = 1;
            statusStrip.Text = "statusStrip1";
            // 
            // errorImageToolStripStatusLabel
            // 
            errorImageToolStripStatusLabel.DisplayStyle = ToolStripItemDisplayStyle.Image;
            errorImageToolStripStatusLabel.Name = "errorImageToolStripStatusLabel";
            errorImageToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            errorImageToolStripStatusLabel.Text = "iii";
            // 
            // errorToolStripStatusLabel
            // 
            errorToolStripStatusLabel.Name = "errorToolStripStatusLabel";
            errorToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // allocUnitProgressBar
            // 
            allocUnitProgressBar.Enabled = false;
            allocUnitProgressBar.Name = "allocUnitProgressBar";
            allocUnitProgressBar.Size = new System.Drawing.Size(117, 16);
            allocUnitProgressBar.Style = ProgressBarStyle.Marquee;
            allocUnitProgressBar.Visible = false;
            // 
            // allocUnitToolStripStatusLabel
            // 
            allocUnitToolStripStatusLabel.Name = "allocUnitToolStripStatusLabel";
            allocUnitToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // spacerToolStripStatusLabel
            // 
            spacerToolStripStatusLabel.Name = "spacerToolStripStatusLabel";
            spacerToolStripStatusLabel.Size = new System.Drawing.Size(1085, 17);
            spacerToolStripStatusLabel.Spring = true;
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new System.Drawing.Size(0, 17);
            // 
            // AllocUnitLabel
            // 
            AllocUnitLabel.Name = "AllocUnitLabel";
            AllocUnitLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // pageAddressToolStripStatusLabel
            // 
            pageAddressToolStripStatusLabel.ForeColor = System.Drawing.Color.Navy;
            pageAddressToolStripStatusLabel.Name = "pageAddressToolStripStatusLabel";
            pageAddressToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // iconToolStripStatusLabel
            // 
            iconToolStripStatusLabel.DisplayStyle = ToolStripItemDisplayStyle.Image;
            iconToolStripStatusLabel.ImageScaling = ToolStripItemImageScaling.None;
            iconToolStripStatusLabel.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            iconToolStripStatusLabel.Name = "iconToolStripStatusLabel";
            iconToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            iconToolStripStatusLabel.Text = "iconToolStripStatusLabel";
            // 
            // allocUnitBackgroundWorker
            // 
            allocUnitBackgroundWorker.WorkerReportsProgress = true;
            allocUnitBackgroundWorker.WorkerSupportsCancellation = true;
            allocUnitBackgroundWorker.DoWork += AllocUnitBackgroundWorker_DoWork;
            allocUnitBackgroundWorker.ProgressChanged += AllocUnitBackgroundWorker_ProgressChanged;
            allocUnitBackgroundWorker.RunWorkerCompleted += AllocUnitBackgroundWorker_RunWorkerCompleted;
            // 
            // keyImageColumn1
            // 
            keyImageColumn1.DataPropertyName = "Colour";
            keyImageColumn1.HeaderText = "";
            keyImageColumn1.MinimumWidth = 12;
            keyImageColumn1.Name = "keyImageColumn1";
            keyImageColumn1.Width = 30;
            // 
            // AllocationWindow
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(splitContainer);
            Controls.Add(statusStrip);
            Margin = new Padding(4);
            Name = "AllocationWindow";
            Size = new System.Drawing.Size(1102, 668);
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            flatMenuStrip.ResumeLayout(false);
            flatMenuStrip.PerformLayout();
            ((ISupportInitialize)keysDataGridView).EndInit();
            ((ISupportInitialize)AllocationBindingSource).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private SplitContainer splitContainer;
        private AllocationContainer allocationContainer;
        private FlatMenuStrip flatMenuStrip;
        private ToolStripLabel toolStripLabel1;
        private ToolStripComboBox databaseToolStripComboBox;
        private ToolStripButton connectToolStripButton;
        private ToolStripSeparator toolStripSeparator1;
        private BackgroundWorker allocUnitBackgroundWorker;
        private ToolStripComboBox extentSizeToolStripComboBox;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripButton bufferPoolToolStripButton;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel errorImageToolStripStatusLabel;
        private ToolStripStatusLabel errorToolStripStatusLabel;
        private ToolStripProgressBar allocUnitProgressBar;
        private ToolStripStatusLabel allocUnitToolStripStatusLabel;
        private ToolStripStatusLabel spacerToolStripStatusLabel;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private ToolStripStatusLabel AllocUnitLabel;
        private ToolStripStatusLabel pageAddressToolStripStatusLabel;
        private ToolStripStatusLabel iconToolStripStatusLabel;
        private BindingSource AllocationBindingSource;
        private DataGridView keysDataGridView;
        private KeyImageColumn keyImageColumn1;
        private PageAddressTextBox pageToolStripTextBox;
        private ToolStripLabel toolStripLabel2;
        private ToolStripButton showKeyToolStripButton;
        private ToolStripButton fileDetailsToolStripButton;
        private ToolStripSplitButton mapToolStripButton;
        private ToolStripMenuItem allocationUnitsToolStripMenuItem;
        private ToolStripMenuItem allocationMapsToolStripMenuItem;
        private ToolStripMenuItem gamToolStripMenuItem;
        private ToolStripMenuItem sgamToolStripMenuItem;
        private ToolStripMenuItem bcmToolStripMenuItem;
        private ToolStripMenuItem dcmToolStripMenuItem;
        private ToolStripMenuItem pFSToolStripMenuItem;
        private KeyImageColumn KeyColumn;
        private DataGridViewTextBoxColumn NameColumn;
        private DataGridViewTextBoxColumn IndexNameColumn;
        private DataGridViewTextBoxColumn IndexTypeColumn;
        private DataGridViewTextBoxColumn RootPageColumn;
        private DataGridViewTextBoxColumn FirstPageColumn;
        private DataGridViewTextBoxColumn FirstIamPageColumn;
        private DataGridViewTextBoxColumn TotalPagesColumn;
    }
}