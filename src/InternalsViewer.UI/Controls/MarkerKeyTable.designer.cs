using System.ComponentModel;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls
{
    partial class MarkerKeyTable
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.markersDataGridView = new System.Windows.Forms.DataGridView();
            this.markerBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.navigateToToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.navigateToInNewWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.pageContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.navigateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.navigateToInNewWindowToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.KeyColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AlternateBackColourColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BackColourColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ForeColourColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.IsNullColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.DataTypeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.nameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.valueDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.startPositionDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.endPositionDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.markersDataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.markerBindingSource)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.pageContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // markersDataGridView
            // 
            this.markersDataGridView.AllowUserToAddRows = false;
            this.markersDataGridView.AllowUserToDeleteRows = false;
            this.markersDataGridView.AllowUserToResizeRows = false;
            this.markersDataGridView.AutoGenerateColumns = false;
            this.markersDataGridView.BackgroundColor = System.Drawing.Color.White;
            this.markersDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.markersDataGridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            this.markersDataGridView.ColumnHeadersHeight = 22;
            this.markersDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.markersDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.KeyColumn,
            this.AlternateBackColourColumn,
            this.BackColourColumn,
            this.ForeColourColumn,
            this.IsNullColumn,
            this.DataTypeColumn,
            this.nameDataGridViewTextBoxColumn,
            this.valueDataGridViewTextBoxColumn,
            this.startPositionDataGridViewTextBoxColumn,
            this.endPositionDataGridViewTextBoxColumn});
            this.markersDataGridView.DataSource = this.markerBindingSource;
            this.markersDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.markersDataGridView.GridColor = System.Drawing.Color.Gainsboro;
            this.markersDataGridView.Location = new System.Drawing.Point(1, 1);
            this.markersDataGridView.Margin = new System.Windows.Forms.Padding(1);
            this.markersDataGridView.Name = "markersDataGridView";
            this.markersDataGridView.ReadOnly = true;
            this.markersDataGridView.RowHeadersVisible = false;
            this.markersDataGridView.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.WhiteSmoke;
            this.markersDataGridView.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
            this.markersDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.markersDataGridView.Size = new System.Drawing.Size(248, 148);
            this.markersDataGridView.TabIndex = 0;
            this.markersDataGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.MarkersDataGridView_CellClick);
            this.markersDataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.MarkersDataGridView_CellContentClick);
            this.markersDataGridView.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.MarkersDataGridView_CellFormatting);
            // 
            // markerBindingSource
            // 
            this.markerBindingSource.DataSource = typeof(InternalsViewer.UI.Markers.Marker);
            this.markerBindingSource.Filter = "visible=true";
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.navigateToToolStripMenuItem,
            this.navigateToInNewWindowToolStripMenuItem,
            this.toolStripSeparator1,
            this.toolStripMenuItem1});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(219, 76);
            // 
            // navigateToToolStripMenuItem
            // 
            this.navigateToToolStripMenuItem.Name = "navigateToToolStripMenuItem";
            this.navigateToToolStripMenuItem.Size = new System.Drawing.Size(218, 22);
            this.navigateToToolStripMenuItem.Text = "Navigate to";
            // 
            // navigateToInNewWindowToolStripMenuItem
            // 
            this.navigateToInNewWindowToolStripMenuItem.Name = "navigateToInNewWindowToolStripMenuItem";
            this.navigateToInNewWindowToolStripMenuItem.Size = new System.Drawing.Size(218, 22);
            this.navigateToInNewWindowToolStripMenuItem.Text = "Navigate to in new window";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(215, 6);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(218, 22);
            this.toolStripMenuItem1.Text = "toolStripMenuItem1";
            // 
            // pageContextMenuStrip
            // 
            this.pageContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.navigateToolStripMenuItem,
            this.navigateToInNewWindowToolStripMenuItem1});
            this.pageContextMenuStrip.Name = "pageContextMenuStrip";
            this.pageContextMenuStrip.Size = new System.Drawing.Size(228, 48);
            // 
            // navigateToolStripMenuItem
            // 
            this.navigateToolStripMenuItem.Name = "navigateToolStripMenuItem";
            this.navigateToolStripMenuItem.Size = new System.Drawing.Size(227, 22);
            this.navigateToolStripMenuItem.Text = "Navigate to...";
            // 
            // navigateToInNewWindowToolStripMenuItem1
            // 
            this.navigateToInNewWindowToolStripMenuItem1.Name = "navigateToInNewWindowToolStripMenuItem1";
            this.navigateToInNewWindowToolStripMenuItem1.Size = new System.Drawing.Size(227, 22);
            this.navigateToInNewWindowToolStripMenuItem1.Text = "Navigate to in new window...";
            // 
            // KeyColumn
            // 
            this.KeyColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.KeyColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.KeyColumn.HeaderText = "";
            this.KeyColumn.Name = "KeyColumn";
            this.KeyColumn.ReadOnly = true;
            this.KeyColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.KeyColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.KeyColumn.ToolTipText = "Key";
            this.KeyColumn.Width = 5;
            // 
            // AlternateBackColourColumn
            // 
            this.AlternateBackColourColumn.DataPropertyName = "AlternateBackColour";
            this.AlternateBackColourColumn.HeaderText = "AlternateBackColour";
            this.AlternateBackColourColumn.Name = "AlternateBackColourColumn";
            this.AlternateBackColourColumn.ReadOnly = true;
            this.AlternateBackColourColumn.Visible = false;
            // 
            // BackColourColumn
            // 
            this.BackColourColumn.DataPropertyName = "BackColour";
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.WhiteSmoke;
            this.BackColourColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.BackColourColumn.HeaderText = "BackColour";
            this.BackColourColumn.Name = "BackColourColumn";
            this.BackColourColumn.ReadOnly = true;
            this.BackColourColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.BackColourColumn.Visible = false;
            // 
            // ForeColourColumn
            // 
            this.ForeColourColumn.DataPropertyName = "ForeColour";
            this.ForeColourColumn.HeaderText = "ForeColour";
            this.ForeColourColumn.Name = "ForeColourColumn";
            this.ForeColourColumn.ReadOnly = true;
            this.ForeColourColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ForeColourColumn.Visible = false;
            // 
            // IsNullColumn
            // 
            this.IsNullColumn.DataPropertyName = "IsNull";
            this.IsNullColumn.HeaderText = "";
            this.IsNullColumn.Name = "IsNullColumn";
            this.IsNullColumn.ReadOnly = true;
            this.IsNullColumn.Visible = false;
            // 
            // DataTypeColumn
            // 
            this.DataTypeColumn.DataPropertyName = "DataType";
            this.DataTypeColumn.HeaderText = "";
            this.DataTypeColumn.Name = "DataTypeColumn";
            this.DataTypeColumn.ReadOnly = true;
            this.DataTypeColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.DataTypeColumn.Visible = false;
            // 
            // nameDataGridViewTextBoxColumn
            // 
            this.nameDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.nameDataGridViewTextBoxColumn.DataPropertyName = "Name";
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.WhiteSmoke;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.Black;
            this.nameDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            this.nameDataGridViewTextBoxColumn.HeaderText = "Name";
            this.nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
            this.nameDataGridViewTextBoxColumn.ReadOnly = true;
            this.nameDataGridViewTextBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.nameDataGridViewTextBoxColumn.ToolTipText = "Item";
            this.nameDataGridViewTextBoxColumn.Width = 41;
            // 
            // valueDataGridViewTextBoxColumn
            // 
            this.valueDataGridViewTextBoxColumn.ActiveLinkColor = System.Drawing.Color.Black;
            this.valueDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.valueDataGridViewTextBoxColumn.DataPropertyName = "Value";
            this.valueDataGridViewTextBoxColumn.HeaderText = "Value";
            this.valueDataGridViewTextBoxColumn.LinkBehavior = System.Windows.Forms.LinkBehavior.NeverUnderline;
            this.valueDataGridViewTextBoxColumn.LinkColor = System.Drawing.Color.Black;
            this.valueDataGridViewTextBoxColumn.Name = "valueDataGridViewTextBoxColumn";
            this.valueDataGridViewTextBoxColumn.ReadOnly = true;
            this.valueDataGridViewTextBoxColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.valueDataGridViewTextBoxColumn.Text = "";
            this.valueDataGridViewTextBoxColumn.ToolTipText = "Value";
            this.valueDataGridViewTextBoxColumn.TrackVisitedState = false;
            this.valueDataGridViewTextBoxColumn.VisitedLinkColor = System.Drawing.Color.Black;
            // 
            // startPositionDataGridViewTextBoxColumn
            // 
            this.startPositionDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.startPositionDataGridViewTextBoxColumn.DataPropertyName = "StartPosition";
            this.startPositionDataGridViewTextBoxColumn.HeaderText = "Start";
            this.startPositionDataGridViewTextBoxColumn.Name = "startPositionDataGridViewTextBoxColumn";
            this.startPositionDataGridViewTextBoxColumn.ReadOnly = true;
            this.startPositionDataGridViewTextBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.startPositionDataGridViewTextBoxColumn.ToolTipText = "Start Offset";
            this.startPositionDataGridViewTextBoxColumn.Width = 35;
            // 
            // endPositionDataGridViewTextBoxColumn
            // 
            this.endPositionDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.endPositionDataGridViewTextBoxColumn.DataPropertyName = "EndPosition";
            this.endPositionDataGridViewTextBoxColumn.HeaderText = "End";
            this.endPositionDataGridViewTextBoxColumn.Name = "endPositionDataGridViewTextBoxColumn";
            this.endPositionDataGridViewTextBoxColumn.ReadOnly = true;
            this.endPositionDataGridViewTextBoxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.endPositionDataGridViewTextBoxColumn.ToolTipText = "End Offset";
            this.endPositionDataGridViewTextBoxColumn.Width = 32;
            // 
            // MarkerKeyTable
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.markersDataGridView);
            this.DoubleBuffered = true;
            this.Name = "MarkerKeyTable";
            this.Padding = new System.Windows.Forms.Padding(1);
            this.Size = new System.Drawing.Size(250, 150);
            ((System.ComponentModel.ISupportInitialize)(this.markersDataGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.markerBindingSource)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.pageContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private DataGridView markersDataGridView;
        private BindingSource markerBindingSource;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem navigateToToolStripMenuItem;
        private ToolStripMenuItem navigateToInNewWindowToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem toolStripMenuItem1;
        private ContextMenuStrip pageContextMenuStrip;
        private ToolStripMenuItem navigateToolStripMenuItem;
        private ToolStripMenuItem navigateToInNewWindowToolStripMenuItem1;
        private DataGridViewTextBoxColumn KeyColumn;
        private DataGridViewTextBoxColumn AlternateBackColourColumn;
        private DataGridViewTextBoxColumn BackColourColumn;
        private DataGridViewTextBoxColumn ForeColourColumn;
        private DataGridViewCheckBoxColumn IsNullColumn;
        private DataGridViewTextBoxColumn DataTypeColumn;
        private DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
        private DataGridViewLinkColumn valueDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn startPositionDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn endPositionDataGridViewTextBoxColumn;
    }
}
