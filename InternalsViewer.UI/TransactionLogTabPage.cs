using System.Data;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using System;

namespace InternalsViewer.UI
{
    public class TransactionLogTabPage : TabPage
    {
        private DataGridViewTextBoxColumn LsnColumn;
        private DataGridViewTextBoxColumn OperationColumn;
        private DataGridViewTextBoxColumn ContextColumn;
        private DataGridViewLinkColumn PageAddressColumn;
        private DataGridViewLinkColumn SlotColumn;
        private DataGridViewTextBoxColumn AllocUnitNameColumn;
        private DataGridViewTextBoxColumn DescriptionColumn;
        private DataGridViewTextBoxColumn isSystemColumn;
        private DataGridViewTextBoxColumn IsAllocationColumn;
        private DataGridView dataGridView;
        public event EventHandler<PageEventArgs> PageClicked;


        public TransactionLogTabPage()
        {
            this.Text = "Transaction Log";

            InitializeComponent();
        }

        public void SetTransactionLogData(DataTable dataTable)
        {
            this.dataGridView.AutoGenerateColumns = false;
            this.dataGridView.DataSource = dataTable;
        }

        private void InitializeComponent()
        {
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.LsnColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OperationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ContextColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PageAddressColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.SlotColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.AllocUnitNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.DescriptionColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.isSystemColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.IsAllocationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.BackgroundColor = System.Drawing.Color.White;
            this.dataGridView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.LsnColumn,
            this.OperationColumn,
            this.ContextColumn,
            this.PageAddressColumn,
            this.SlotColumn,
            this.AllocUnitNameColumn,
            this.DescriptionColumn,
            this.isSystemColumn,
            this.IsAllocationColumn});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.GridColor = System.Drawing.SystemColors.Control;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Margin = new System.Windows.Forms.Padding(0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.WhiteSmoke;
            this.dataGridView.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
            this.dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView.Size = new System.Drawing.Size(200, 100);
            this.dataGridView.TabIndex = 1;
            this.dataGridView.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dataGridView_CellFormatting);
            this.dataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridView_CellContentClick);
            // 
            // LsnColumn
            // 
            this.LsnColumn.DataPropertyName = "LSN";
            this.LsnColumn.HeaderText = "LSN";
            this.LsnColumn.Name = "LsnColumn";
            this.LsnColumn.ReadOnly = true;
            this.LsnColumn.Width = 134;
            // 
            // OperationColumn
            // 
            this.OperationColumn.DataPropertyName = "operation";
            this.OperationColumn.HeaderText = "Operation";
            this.OperationColumn.Name = "OperationColumn";
            this.OperationColumn.ReadOnly = true;
            this.OperationColumn.Width = 105;
            // 
            // ContextColumn
            // 
            this.ContextColumn.DataPropertyName = "context";
            this.ContextColumn.HeaderText = "Context";
            this.ContextColumn.Name = "ContextColumn";
            this.ContextColumn.ReadOnly = true;
            // 
            // PageAddressColumn
            // 
            this.PageAddressColumn.ActiveLinkColor = System.Drawing.Color.Blue;
            this.PageAddressColumn.DataPropertyName = "PageAddress";
            this.PageAddressColumn.HeaderText = "Page";
            this.PageAddressColumn.Name = "PageAddressColumn";
            this.PageAddressColumn.ReadOnly = true;
            this.PageAddressColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.PageAddressColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.PageAddressColumn.TrackVisitedState = false;
            this.PageAddressColumn.VisitedLinkColor = System.Drawing.Color.Blue;
            this.PageAddressColumn.Width = 80;
            // 
            // SlotColumn
            // 
            this.SlotColumn.ActiveLinkColor = System.Drawing.Color.Blue;
            this.SlotColumn.DataPropertyName = "SlotId";
            this.SlotColumn.HeaderText = "Slot";
            this.SlotColumn.Name = "SlotColumn";
            this.SlotColumn.ReadOnly = true;
            this.SlotColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.SlotColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.SlotColumn.TrackVisitedState = false;
            this.SlotColumn.VisitedLinkColor = System.Drawing.Color.Blue;
            this.SlotColumn.Width = 50;
            // 
            // AllocUnitNameColumn
            // 
            this.AllocUnitNameColumn.DataPropertyName = "AllocUnitName";
            this.AllocUnitNameColumn.FillWeight = 190.4762F;
            this.AllocUnitNameColumn.HeaderText = "Allocation Unit";
            this.AllocUnitNameColumn.Name = "AllocUnitNameColumn";
            this.AllocUnitNameColumn.ReadOnly = true;
            this.AllocUnitNameColumn.Width = 120;
            // 
            // DescriptionColumn
            // 
            this.DescriptionColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.DescriptionColumn.DataPropertyName = "Description";
            this.DescriptionColumn.FillWeight = 9.523804F;
            this.DescriptionColumn.HeaderText = "Description";
            this.DescriptionColumn.Name = "DescriptionColumn";
            this.DescriptionColumn.ReadOnly = true;
            // 
            // isSystemColumn
            // 
            this.isSystemColumn.DataPropertyName = "isSystem";
            this.isSystemColumn.HeaderText = "System";
            this.isSystemColumn.Name = "isSystemColumn";
            this.isSystemColumn.ReadOnly = true;
            this.isSystemColumn.Visible = false;
            // 
            // IsAllocationColumn
            // 
            this.IsAllocationColumn.DataPropertyName = "isAllocation";
            this.IsAllocationColumn.HeaderText = "Allocation";
            this.IsAllocationColumn.Name = "IsAllocationColumn";
            this.IsAllocationColumn.ReadOnly = true;
            this.IsAllocationColumn.Visible = false;
            // 
            // TransactionLogTabPage
            // 
            this.Controls.Add(this.dataGridView);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        void dataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            bool isSystem = (bool)dataGridView["isSystemColumn", e.RowIndex].Value;
            bool isAllocation = (bool)dataGridView["isAllocationColumn", e.RowIndex].Value;

            if (isSystem & !isAllocation)
            {
                e.CellStyle.ForeColor = Color.DimGray;
            }
            else if (isAllocation)
            {
                e.CellStyle.ForeColor = Color.DarkOliveGreen;
            }

            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
        }

        private void DataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView.Columns[e.ColumnIndex].DataPropertyName == "PageAddress" ||
                dataGridView.Columns[e.ColumnIndex].DataPropertyName == "SlotId")
            {
                PageAddress pageAddress = PageAddress.Parse(dataGridView[3, e.RowIndex].Value.ToString());

                int slot = (int)dataGridView[4, e.RowIndex].Value;

                this.OnPageClicked(sender, new PageEventArgs(new RowIdentifier(pageAddress, slot), false));
            }
        }

        internal virtual void OnPageClicked(object sender, PageEventArgs e)
        {
            if (this.PageClicked != null)
            {
                this.PageClicked(sender, e);
            }

        }
    }
}
