using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Pages;


#pragma warning disable CA1416

namespace InternalsViewer.UI;

public partial class OffsetTable : UserControl
{
    private Page? page;
    public event EventHandler? SlotChanged;

    public OffsetTable()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Constructs a DataTable for display purposes from the offset list
    /// </summary>
    /// <param name="offsetTable">The offset table.</param>
    /// <returns></returns>
    public static DataTable ConstructOffsetTable(List<ushort> offsetTable)
    {
        var offsetDataTable = new DataTable();

        var slotColumn = new DataColumn("Slot", typeof(ushort));
        var offsetColumn = new DataColumn("Offset", typeof(ushort));
        var offsetHexColumn = new DataColumn("Hex", typeof(string));

        offsetDataTable.Columns.Add(slotColumn);
        offsetDataTable.Columns.Add(offsetColumn);
        offsetDataTable.Columns.Add(offsetHexColumn);

        var slotPosition = 0;

        if (offsetTable != null)
        {
            foreach (int i in offsetTable)
            {
                var offsetRow = offsetDataTable.NewRow();

                offsetRow["Slot"] = slotPosition;
                offsetRow["Offset"] = i;
                offsetRow["Hex"] = string.Concat("0x", DataConverter.ToHexString(BitConverter.GetBytes((short)i)));

                offsetDataTable.Rows.Add(offsetRow);

                slotPosition++;
            }
        }

        return offsetDataTable;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlPaint.DrawBorder(e.Graphics,
            new Rectangle(0, 0, Width, Height),
            SystemColors.ControlDark,
            ButtonBorderStyle.Solid);
    }

    internal virtual void OnSlotChanged(object sender, EventArgs e)
    {
        SlotChanged?.Invoke(sender, e);
    }

    /// <summary>
    /// Gets or sets the parent page.
    /// </summary>
    /// <value>The page.</value>
    public Page? Page
    {
        get => page;
        set
        {
            page = value;

            if (Page != null)
            {
                offsetDataGridView.DataSource = ConstructOffsetTable(Page.OffsetTable);
            }
        }
    }

    /// <summary>
    /// Gets the selected offset.
    /// </summary>
    /// <value>The selected offset.</value>
    public ushort SelectedOffset
    {
        get
        {
            if (offsetDataGridView.SelectedRows.Count > 0)
            {
                return ushort.Parse(offsetDataGridView.SelectedRows[0].Cells[1].Value?.ToString() ?? string.Empty);
            }

            return 0;
        }
    }

    public int SelectedSlot
    {
        get
        {
            if (offsetDataGridView.SelectedRows.Count > 0)
            {
                return offsetDataGridView.SelectedRows[0].Index;
            }

            return -1;
        }
        set
        {
            if (value >= 0 && offsetDataGridView.SelectedRows.Count > 0)
            {
                offsetDataGridView.Rows[value].Selected = true;
                offsetDataGridView.FirstDisplayedScrollingRowIndex = offsetDataGridView.SelectedRows[0].Index;
            }
            else if (value < 0)
            {
                offsetDataGridView.ClearSelection();
            }
        }
    }

}