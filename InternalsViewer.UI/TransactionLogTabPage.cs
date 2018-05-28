using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals;
using System.Collections.Generic;
using InternalsViewer.Internals.TransactionLog;

namespace InternalsViewer.UI
{
    public class TransactionLogTabPage : TabPage
    {
        private DataGridView dataGridView;
        private DataGridViewTextBoxColumn lsnColumn;
        private DataGridViewTextBoxColumn operationColumn;
        private DataGridViewTextBoxColumn contextColumn;
        private DataGridViewLinkColumn pageAddressColumn;
        private DataGridViewLinkColumn slotColumn;
        private DataGridViewTextBoxColumn allocUnitNameColumn;
        private DataGridViewTextBoxColumn descriptionColumn;
        private DataGridViewTextBoxColumn isSystemColumn;
        private DataGridViewTextBoxColumn isAllocationColumn;

        public event EventHandler<PageEventArgs> PageClicked;

        public TransactionLogTabPage()
        {
            this.Text = "Transaction Log";
            this.LogContents = new Dictionary<string, LogData>();

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
            this.lsnColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.operationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.contextColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.pageAddressColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.slotColumn = new System.Windows.Forms.DataGridViewLinkColumn();
            this.allocUnitNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.descriptionColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.isSystemColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.isAllocationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
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
            this.lsnColumn,
            this.operationColumn,
            this.contextColumn,
            this.pageAddressColumn,
            this.slotColumn,
            this.allocUnitNameColumn,
            this.descriptionColumn,
            this.isSystemColumn,
            this.isAllocationColumn});
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.GridColor = System.Drawing.SystemColors.Control;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Margin = new System.Windows.Forms.Padding(0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.RowHeadersVisible = false;
            this.dataGridView.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.WhiteSmoke;
            this.dataGridView.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
            this.dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView.Size = new System.Drawing.Size(200, 100);
            this.dataGridView.TabIndex = 1;
            this.dataGridView.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.DataGridView_CellFormatting);
            this.dataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridView_CellContentClick);
            // 
            // LsnColumn
            // 
            this.lsnColumn.DataPropertyName = "LSN";
            this.lsnColumn.HeaderText = "LSN";
            this.lsnColumn.Name = "lsnColumn";
            this.lsnColumn.ReadOnly = true;
            this.lsnColumn.Width = 134;
            // 
            // OperationColumn
            // 
            this.operationColumn.DataPropertyName = "operation";
            this.operationColumn.HeaderText = "Operation";
            this.operationColumn.Name = "operationColumn";
            this.operationColumn.ReadOnly = true;
            this.operationColumn.Width = 105;
            // 
            // ContextColumn
            // 
            this.contextColumn.DataPropertyName = "context";
            this.contextColumn.HeaderText = "Context";
            this.contextColumn.Name = "contextColumn";
            this.contextColumn.ReadOnly = true;
            // 
            // PageAddressColumn
            // 
            this.pageAddressColumn.ActiveLinkColor = System.Drawing.Color.Blue;
            this.pageAddressColumn.DataPropertyName = "PageAddress";
            this.pageAddressColumn.HeaderText = "Page";
            this.pageAddressColumn.Name = "pageAddressColumn";
            this.pageAddressColumn.ReadOnly = true;
            this.pageAddressColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.pageAddressColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.pageAddressColumn.TrackVisitedState = false;
            this.pageAddressColumn.VisitedLinkColor = System.Drawing.Color.Blue;
            this.pageAddressColumn.Width = 80;
            // 
            // SlotColumn
            // 
            this.slotColumn.ActiveLinkColor = System.Drawing.Color.Blue;
            this.slotColumn.DataPropertyName = "SlotId";
            this.slotColumn.HeaderText = "Slot";
            this.slotColumn.Name = "slotColumn";
            this.slotColumn.ReadOnly = true;
            this.slotColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.slotColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.slotColumn.TrackVisitedState = false;
            this.slotColumn.VisitedLinkColor = System.Drawing.Color.Blue;
            this.slotColumn.Width = 50;
            // 
            // AllocUnitNameColumn
            // 
            this.allocUnitNameColumn.DataPropertyName = "AllocUnitName";
            this.allocUnitNameColumn.FillWeight = 190.4762F;
            this.allocUnitNameColumn.HeaderText = "Allocation Unit";
            this.allocUnitNameColumn.Name = "allocUnitNameColumn";
            this.allocUnitNameColumn.ReadOnly = true;
            this.allocUnitNameColumn.Width = 120;
            // 
            // DescriptionColumn
            // 
            this.descriptionColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.descriptionColumn.DataPropertyName = "Description";
            this.descriptionColumn.FillWeight = 9.523804F;
            this.descriptionColumn.HeaderText = "Description";
            this.descriptionColumn.Name = "descriptionColumn";
            this.descriptionColumn.ReadOnly = true;
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
            this.isAllocationColumn.DataPropertyName = "isAllocation";
            this.isAllocationColumn.HeaderText = "Allocation";
            this.isAllocationColumn.Name = "isAllocationColumn";
            this.isAllocationColumn.ReadOnly = true;
            this.isAllocationColumn.Visible = false;
            // 
            // TransactionLogTabPage
            // 
            this.Controls.Add(this.dataGridView);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        /// <summary>
        /// Handles the CellFormatting event of the DataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.DataGridViewCellFormattingEventArgs"/> instance containing the event data.</param>
        void DataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var isSystem = (bool)dataGridView["isSystemColumn", e.RowIndex].Value;
            var isAllocation = (bool)dataGridView["isAllocationColumn", e.RowIndex].Value;

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

        /// <summary>
        /// Handles the CellContentClick event of the DataGridView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.DataGridViewCellEventArgs"/> instance containing the event data.</param>
        private void DataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView.Columns[e.ColumnIndex].DataPropertyName == "PageAddress" ||
                dataGridView.Columns[e.ColumnIndex].DataPropertyName == "SlotId")
            {
                var pageAddress = PageAddress.Parse(dataGridView[3, e.RowIndex].Value.ToString());

                var slot = (int)dataGridView[4, e.RowIndex].Value;

                SetSelectedLogContents(e.RowIndex);

                this.OnPageClicked(sender, new PageEventArgs(new RowIdentifier(pageAddress, slot), false));
            }
        }

        private void SetSelectedLogContents(int rowId)
        {
            GetLogData(dataGridView.Rows[rowId].Cells["OperationColumn"].Value.ToString(), dataGridView.Rows[rowId]);
        }

        /// <summary>
        /// Called when [page clicked].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        internal virtual void OnPageClicked(object sender, PageEventArgs e)
        {
            if (this.PageClicked != null)
            {
                this.PageClicked(this, e);
            }

        }

        private void SetSelectedLogContents(PageAddress address)
        {
            this.LogContents.Clear();

            foreach (DataGridViewRow row in this.dataGridView.Rows)
            {
                if (row.Cells["PageAddressColumn"].Value != DBNull.Value && (PageAddress)row.Cells["PageAddressColumn"].Value == address)
                {
                    GetLogData(row.Cells["OperationColumn"].Value.ToString(), row);
                }
            }
        }

        private void GetLogData(string operation, DataGridViewRow row)
        {
            this.LogContents.Clear();

            switch (operation)
            {
                case "MODIFY ROW":

                    this.LogContents.Add("Before", GetLogData(row, 0));
                    this.LogContents.Add("After", GetLogData(row, 1));

                    break;

                case "INSERT ROWS":
                    this.LogContents.Add("Before", GetLogData(row, 0));
                    break;
            }
        }

        private LogData GetLogData(DataGridViewRow row, int contentsIndex)
        {
            var logData = new LogData();

            logData.Slot = Convert.ToUInt16((row.DataBoundItem as DataRowView)["SlotId"]);
            logData.Offset = Convert.ToUInt16((row.DataBoundItem as DataRowView)["OffsetInRow"]);
            // logData.LogSequenceNumber = new LogSequenceNumber((row.DataBoundItem as DataRowView)["LSN"].ToString());
            logData.Data = (byte[])(row.DataBoundItem as DataRowView)["Contents" + contentsIndex];

            System.Diagnostics.Debug.Print(logData.ToString());

            return logData;
        }

        public Dictionary<string, LogData> LogContents { get; set; }
    }
}
