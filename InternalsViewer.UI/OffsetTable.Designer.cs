using System.ComponentModel;
using System.Windows.Forms;

namespace InternalsViewer.UI
{
    partial class OffsetTable
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.offsetDataGridView = new System.Windows.Forms.DataGridView();
            this.SlotDataGridViewColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.HexOffsetGridViewColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OffsetGridViewColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.offsetDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // offsetDataGridView
            // 
            this.offsetDataGridView.AllowUserToAddRows = false;
            this.offsetDataGridView.AllowUserToDeleteRows = false;
            this.offsetDataGridView.AllowUserToResizeRows = false;
            this.offsetDataGridView.BackgroundColor = System.Drawing.Color.White;
            this.offsetDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.offsetDataGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            this.offsetDataGridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.offsetDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.offsetDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.offsetDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.SlotDataGridViewColumn,
            this.HexOffsetGridViewColumn,
            this.OffsetGridViewColumn});
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.offsetDataGridView.DefaultCellStyle = dataGridViewCellStyle3;
            this.offsetDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.offsetDataGridView.GridColor = System.Drawing.Color.Gainsboro;
            this.offsetDataGridView.Location = new System.Drawing.Point(0, 0);
            this.offsetDataGridView.MultiSelect = false;
            this.offsetDataGridView.Name = "offsetDataGridView";
            this.offsetDataGridView.ReadOnly = true;
            this.offsetDataGridView.RowHeadersVisible = false;
            this.offsetDataGridView.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            this.offsetDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.offsetDataGridView.Size = new System.Drawing.Size(150, 150);
            this.offsetDataGridView.TabIndex = 4;
            this.offsetDataGridView.SelectionChanged += new System.EventHandler(this.OnSlotChanged);
            // 
            // SlotDataGridViewColumn
            // 
            this.SlotDataGridViewColumn.DataPropertyName = "slot";
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.WhiteSmoke;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.WhiteSmoke;
            this.SlotDataGridViewColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.SlotDataGridViewColumn.HeaderText = "Slot";
            this.SlotDataGridViewColumn.Name = "SlotDataGridViewColumn";
            this.SlotDataGridViewColumn.ReadOnly = true;
            this.SlotDataGridViewColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.SlotDataGridViewColumn.Width = 40;
            // 
            // HexOffsetGridViewColumn
            // 
            this.HexOffsetGridViewColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.HexOffsetGridViewColumn.DataPropertyName = "hex";
            this.HexOffsetGridViewColumn.HeaderText = "Offset";
            this.HexOffsetGridViewColumn.Name = "HexOffsetGridViewColumn";
            this.HexOffsetGridViewColumn.ReadOnly = true;
            this.HexOffsetGridViewColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // OffsetGridViewColumn
            // 
            this.OffsetGridViewColumn.DataPropertyName = "offset";
            this.OffsetGridViewColumn.HeaderText = "";
            this.OffsetGridViewColumn.Name = "OffsetGridViewColumn";
            this.OffsetGridViewColumn.ReadOnly = true;
            this.OffsetGridViewColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.OffsetGridViewColumn.Width = 40;
            // 
            // OffsetTable
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.offsetDataGridView);
            this.Name = "OffsetTable";
            ((System.ComponentModel.ISupportInitialize)(this.offsetDataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DataGridView offsetDataGridView;
        private DataGridViewTextBoxColumn SlotDataGridViewColumn;
        private DataGridViewTextBoxColumn HexOffsetGridViewColumn;
        private DataGridViewTextBoxColumn OffsetGridViewColumn;
    }
}
