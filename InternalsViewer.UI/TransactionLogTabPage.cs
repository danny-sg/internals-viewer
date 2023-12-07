using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.TransactionLog;

namespace InternalsViewer.UI;

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
        Text = "Transaction Log";
        LogContents = new Dictionary<string, LogData>();

        InitializeComponent();
    }

    public void SetTransactionLogData(DataTable dataTable)
    {
        dataGridView.AutoGenerateColumns = false;
        dataGridView.DataSource = dataTable;
    }

    private void InitializeComponent()
    {
        dataGridView = new System.Windows.Forms.DataGridView();
        lsnColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        operationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        contextColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        pageAddressColumn = new System.Windows.Forms.DataGridViewLinkColumn();
        slotColumn = new System.Windows.Forms.DataGridViewLinkColumn();
        allocUnitNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        descriptionColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        isSystemColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        isAllocationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        ((System.ComponentModel.ISupportInitialize)(dataGridView)).BeginInit();
        SuspendLayout();
        // 
        // dataGridView
        // 
        dataGridView.AllowUserToAddRows = false;
        dataGridView.AllowUserToDeleteRows = false;
        dataGridView.AllowUserToResizeRows = false;
        dataGridView.BackgroundColor = System.Drawing.Color.White;
        dataGridView.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
        dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            lsnColumn,
            operationColumn,
            contextColumn,
            pageAddressColumn,
            slotColumn,
            allocUnitNameColumn,
            descriptionColumn,
            isSystemColumn,
            isAllocationColumn});
        dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
        dataGridView.GridColor = System.Drawing.SystemColors.Control;
        dataGridView.Location = new System.Drawing.Point(0, 0);
        dataGridView.Margin = new System.Windows.Forms.Padding(0);
        dataGridView.Name = "dataGridView";
        dataGridView.RowHeadersVisible = false;
        dataGridView.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.WhiteSmoke;
        dataGridView.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
        dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        dataGridView.Size = new System.Drawing.Size(200, 100);
        dataGridView.TabIndex = 1;
        dataGridView.CellFormatting += DataGridView_CellFormatting;
        dataGridView.CellContentClick += DataGridView_CellContentClick;
        // 
        // LsnColumn
        // 
        lsnColumn.DataPropertyName = "LSN";
        lsnColumn.HeaderText = "LSN";
        lsnColumn.Name = "lsnColumn";
        lsnColumn.ReadOnly = true;
        lsnColumn.Width = 134;
        // 
        // OperationColumn
        // 
        operationColumn.DataPropertyName = "operation";
        operationColumn.HeaderText = "Operation";
        operationColumn.Name = "operationColumn";
        operationColumn.ReadOnly = true;
        operationColumn.Width = 105;
        // 
        // ContextColumn
        // 
        contextColumn.DataPropertyName = "context";
        contextColumn.HeaderText = "Context";
        contextColumn.Name = "contextColumn";
        contextColumn.ReadOnly = true;
        // 
        // PageAddressColumn
        // 
        pageAddressColumn.ActiveLinkColor = System.Drawing.Color.Blue;
        pageAddressColumn.DataPropertyName = "PageAddress";
        pageAddressColumn.HeaderText = "Page";
        pageAddressColumn.Name = "pageAddressColumn";
        pageAddressColumn.ReadOnly = true;
        pageAddressColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
        pageAddressColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
        pageAddressColumn.TrackVisitedState = false;
        pageAddressColumn.VisitedLinkColor = System.Drawing.Color.Blue;
        pageAddressColumn.Width = 80;
        // 
        // SlotColumn
        // 
        slotColumn.ActiveLinkColor = System.Drawing.Color.Blue;
        slotColumn.DataPropertyName = "SlotId";
        slotColumn.HeaderText = "Slot";
        slotColumn.Name = "slotColumn";
        slotColumn.ReadOnly = true;
        slotColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
        slotColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
        slotColumn.TrackVisitedState = false;
        slotColumn.VisitedLinkColor = System.Drawing.Color.Blue;
        slotColumn.Width = 50;
        // 
        // AllocUnitNameColumn
        // 
        allocUnitNameColumn.DataPropertyName = "AllocUnitName";
        allocUnitNameColumn.FillWeight = 190.4762F;
        allocUnitNameColumn.HeaderText = "Allocation Unit";
        allocUnitNameColumn.Name = "allocUnitNameColumn";
        allocUnitNameColumn.ReadOnly = true;
        allocUnitNameColumn.Width = 120;
        // 
        // DescriptionColumn
        // 
        descriptionColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
        descriptionColumn.DataPropertyName = "Description";
        descriptionColumn.FillWeight = 9.523804F;
        descriptionColumn.HeaderText = "Description";
        descriptionColumn.Name = "descriptionColumn";
        descriptionColumn.ReadOnly = true;
        // 
        // isSystemColumn
        // 
        isSystemColumn.DataPropertyName = "isSystem";
        isSystemColumn.HeaderText = "System";
        isSystemColumn.Name = "isSystemColumn";
        isSystemColumn.ReadOnly = true;
        isSystemColumn.Visible = false;
        // 
        // IsAllocationColumn
        // 
        isAllocationColumn.DataPropertyName = "isAllocation";
        isAllocationColumn.HeaderText = "Allocation";
        isAllocationColumn.Name = "isAllocationColumn";
        isAllocationColumn.ReadOnly = true;
        isAllocationColumn.Visible = false;
        // 
        // TransactionLogTabPage
        // 
        Controls.Add(dataGridView);
        ((System.ComponentModel.ISupportInitialize)(dataGridView)).EndInit();
        ResumeLayout(false);

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

            OnPageClicked(sender, new PageEventArgs(new RowIdentifier(pageAddress, slot), false));
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
        if (PageClicked != null)
        {
            PageClicked(this, e);
        }

    }

    private void SetSelectedLogContents(PageAddress address)
    {
        LogContents.Clear();

        foreach (DataGridViewRow row in dataGridView.Rows)
        {
            if (row.Cells["PageAddressColumn"].Value != DBNull.Value && (PageAddress)row.Cells["PageAddressColumn"].Value == address)
            {
                GetLogData(row.Cells["OperationColumn"].Value.ToString(), row);
            }
        }
    }

    private void GetLogData(string operation, DataGridViewRow row)
    {
        LogContents.Clear();

        switch (operation)
        {
            case "MODIFY ROW":

                LogContents.Add("Before", GetLogData(row, 0));
                LogContents.Add("After", GetLogData(row, 1));

                break;

            case "INSERT ROWS":
                LogContents.Add("Before", GetLogData(row, 0));
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