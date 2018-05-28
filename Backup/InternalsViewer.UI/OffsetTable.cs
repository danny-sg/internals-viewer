using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI
{
    public partial class OffsetTable : UserControl
    {
        private Page page;
        public event EventHandler SlotChanged;

        public OffsetTable()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructs a DataTable for display purposes from the offset list
        /// </summary>
        /// <param name="offsetTable">The offset table.</param>
        /// <returns></returns>
        public static DataTable ConstructOffsetTable(List<UInt16> offsetTable)
        {
            DataTable offsetDataTable = new DataTable();

            DataColumn slotColumn = new DataColumn("Slot", typeof(UInt16));
            DataColumn offsetColumn = new DataColumn("Offset", typeof(UInt16));
            DataColumn offsetHexColumn = new DataColumn("Hex", typeof(string));

            offsetDataTable.Columns.Add(slotColumn);
            offsetDataTable.Columns.Add(offsetColumn);
            offsetDataTable.Columns.Add(offsetHexColumn);

            int slotPosition = 0;

            if (offsetTable != null)
            {
                foreach (int i in offsetTable)
                {
                    DataRow offsetRow = offsetDataTable.NewRow();

                    offsetRow["Slot"] = slotPosition;
                    offsetRow["Offset"] = i;
                    offsetRow["Hex"] = string.Concat("0x", DataConverter.ToHexString(BitConverter.GetBytes((Int16)i)));

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
                                    new Rectangle(0, 0, this.Width, this.Height),
                                    SystemColors.ControlDark,
                                    ButtonBorderStyle.Solid);
        }

        internal virtual void OnSlotChanged(object sender, EventArgs e)
        {
            if (this.SlotChanged != null)
            {
                this.SlotChanged(sender, e);
            }
        }

        /// <summary>
        /// Gets or sets the parent page.
        /// </summary>
        /// <value>The page.</value>
        public Page Page
        {
            get
            {
                return page;
            }
            set
            {
                this.page = value;

                if (this.Page != null)
                {
                    this.offsetDataGridView.DataSource = ConstructOffsetTable(this.Page.OffsetTable);
                }
            }
        }

        /// <summary>
        /// Gets the selected offset.
        /// </summary>
        /// <value>The selected offset.</value>
        public UInt16 SelectedOffset
        {
            get
            {
                if (this.offsetDataGridView.SelectedRows.Count > 0)
                {
                    return UInt16.Parse(this.offsetDataGridView.SelectedRows[0].Cells[1].Value.ToString());
                }
                else
                {

                    return 0;
                }
            }
        }

        public int SelectedSlot
        {
            get
            {
                if (this.offsetDataGridView.SelectedRows.Count > 0)
                {
                    return this.offsetDataGridView.SelectedRows[0].Index;
                }
                else
                {
                    return -1;
                }
            }
            set
            {
                if (value >= 0 && this.offsetDataGridView.SelectedRows.Count > 0)
                {
                    this.offsetDataGridView.Rows[value].Selected = true;
                    this.offsetDataGridView.FirstDisplayedScrollingRowIndex = this.offsetDataGridView.SelectedRows[0].Index;
                }
                else if (value < 0)
                {
                    this.offsetDataGridView.ClearSelection();
                }
            }
        }

    }
}
