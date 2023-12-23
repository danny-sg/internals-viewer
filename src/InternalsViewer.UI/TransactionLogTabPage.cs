using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.TransactionLog;

#pragma warning disable CA1416

namespace InternalsViewer.UI;

public class TransactionLogTabPage : TabPage
{
    private DataGridView dataGridView = null!;
    private DataGridViewTextBoxColumn lsnColumn = null!;
    private DataGridViewTextBoxColumn operationColumn = null!;
    private DataGridViewTextBoxColumn contextColumn = null!;
    private DataGridViewLinkColumn pageAddressColumn = null!;
    private DataGridViewLinkColumn slotColumn = null!;
    private DataGridViewTextBoxColumn allocUnitNameColumn = null!;
    private DataGridViewTextBoxColumn descriptionColumn = null!;
    private DataGridViewTextBoxColumn isSystemColumn = null!;
    private DataGridViewTextBoxColumn isAllocationColumn = null!;

    public event EventHandler<PageEventArgs>? PageClicked;

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
        dataGridView = new DataGridView();
        lsnColumn = new DataGridViewTextBoxColumn();
        operationColumn = new DataGridViewTextBoxColumn();
        contextColumn = new DataGridViewTextBoxColumn();
        pageAddressColumn = new DataGridViewLinkColumn();
        slotColumn = new DataGridViewLinkColumn();
        allocUnitNameColumn = new DataGridViewTextBoxColumn();
        descriptionColumn = new DataGridViewTextBoxColumn();
        isSystemColumn = new DataGridViewTextBoxColumn();
        isAllocationColumn = new DataGridViewTextBoxColumn();
        ((ISupportInitialize)(dataGridView)).BeginInit();
        SuspendLayout();
        // 
        // dataGridView
        // 
        dataGridView.AllowUserToAddRows = false;
        dataGridView.AllowUserToDeleteRows = false;
        dataGridView.AllowUserToResizeRows = false;
        dataGridView.BackgroundColor = Color.White;
        dataGridView.BorderStyle = BorderStyle.Fixed3D;
        dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dataGridView.Columns.AddRange(lsnColumn, operationColumn, contextColumn, pageAddressColumn, slotColumn, allocUnitNameColumn, descriptionColumn, isSystemColumn, isAllocationColumn);
        dataGridView.Dock = DockStyle.Fill;
        dataGridView.GridColor = SystemColors.Control;
        dataGridView.Location = new Point(0, 0);
        dataGridView.Margin = new Padding(0);
        dataGridView.Name = "dataGridView";
        dataGridView.RowHeadersVisible = false;
        dataGridView.RowTemplate.DefaultCellStyle.SelectionBackColor = Color.WhiteSmoke;
        dataGridView.RowTemplate.DefaultCellStyle.SelectionForeColor = Color.Black;
        dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dataGridView.Size = new Size(200, 100);
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
        pageAddressColumn.ActiveLinkColor = Color.Blue;
        pageAddressColumn.DataPropertyName = "PageAddress";
        pageAddressColumn.HeaderText = "Page";
        pageAddressColumn.Name = "pageAddressColumn";
        pageAddressColumn.ReadOnly = true;
        pageAddressColumn.Resizable = DataGridViewTriState.True;
        pageAddressColumn.SortMode = DataGridViewColumnSortMode.Automatic;
        pageAddressColumn.TrackVisitedState = false;
        pageAddressColumn.VisitedLinkColor = Color.Blue;
        pageAddressColumn.Width = 80;
        // 
        // SlotColumn
        // 
        slotColumn.ActiveLinkColor = Color.Blue;
        slotColumn.DataPropertyName = "SlotId";
        slotColumn.HeaderText = "Slot";
        slotColumn.Name = "slotColumn";
        slotColumn.ReadOnly = true;
        slotColumn.Resizable = DataGridViewTriState.True;
        slotColumn.SortMode = DataGridViewColumnSortMode.Automatic;
        slotColumn.TrackVisitedState = false;
        slotColumn.VisitedLinkColor = Color.Blue;
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
        descriptionColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
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
        ((ISupportInitialize)(dataGridView)).EndInit();
        ResumeLayout(false);

    }

    /// <summary>
    /// Handles the CellFormatting event of the DataGridView control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Forms.DataGridViewCellFormattingEventArgs"/> instance containing the event data.</param>
    void DataGridView_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.CellStyle == null)
        {
            return;
        }

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
    private void DataGridView_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (dataGridView.Columns[e.ColumnIndex].DataPropertyName == "PageAddress" ||
            dataGridView.Columns[e.ColumnIndex].DataPropertyName == "SlotId")
        {
            var pageAddress = PageAddressParser.Parse(dataGridView[3, e.RowIndex].Value.ToString() ?? string.Empty);

            var slot = (int)dataGridView[4, e.RowIndex].Value;

            SetSelectedLogContents(e.RowIndex);

            OnPageClicked(sender, new PageEventArgs(new RowIdentifier(pageAddress, slot), false));
        }
    }

    private void SetSelectedLogContents(int rowId)
    {
        GetLogData(dataGridView.Rows[rowId].Cells["OperationColumn"].Value.ToString() ?? string.Empty, dataGridView.Rows[rowId]);
    }

    /// <summary>
    /// Called when [page clicked].
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="PageEventArgs"/> instance containing the event data.</param>
    internal virtual void OnPageClicked(object? sender, PageEventArgs e)
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
                GetLogData(row.Cells["OperationColumn"].Value.ToString() ?? string.Empty, row);
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

        var item = (row.DataBoundItem as DataRowView);

        if (item == null)
        {
            return logData;
        }

        logData.Slot = Convert.ToUInt16(item["SlotId"]);
        logData.Offset = Convert.ToUInt16(item["OffsetInRow"]);
        // logData.LogSequenceNumber = new LogSequenceNumber((row.DataBoundItem as DataRowView)["LSN"].ToString());
        logData.Data = (byte[])item["Contents" + contentsIndex];

        Debug.Print(logData.ToString());

        return logData;
    }

    public Dictionary<string, LogData> LogContents { get; set; }
}