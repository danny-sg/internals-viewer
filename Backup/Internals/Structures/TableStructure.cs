using System;
using System.Data;
using System.Data.SqlClient;

namespace InternalsViewer.Internals.Structures
{
    public class TableStructure : Structure
    {
        private int columnCount;

        public TableStructure(long allocationUnitId, Database database)
            : base(allocationUnitId, database)
        {
            this.AddColumns(this.StructureDataTable);
        }

        internal override void AddColumns(DataTable structure)
        {
            Column currentColumn;

            if (structure.Rows.Count > 0)
            {
                foreach (DataRow tableColumn in structure.Rows)
                {
                    columnCount++;

                    currentColumn = new Column();

                    currentColumn.ColumnName = tableColumn["name"].ToString();
                    currentColumn.ColumnId = Convert.ToInt32(tableColumn["column_id"]);
                    currentColumn.DataType = DataConverter.ToSqlType(Convert.ToByte(tableColumn["system_type_id"]));
                    currentColumn.DataLength = Convert.ToInt16(tableColumn["max_length"]);
                    currentColumn.LeafOffset = Convert.ToInt16(tableColumn["leaf_offset"]);
                    currentColumn.Precision = Convert.ToByte(tableColumn["precision"]);
                    currentColumn.Scale = Convert.ToByte(tableColumn["scale"]);
                    currentColumn.Dropped = Convert.ToBoolean(tableColumn["is_dropped"]);
                    currentColumn.Uniqueifer = Convert.ToBoolean(tableColumn["is_uniqueifier"]);
                    currentColumn.Sparse = Convert.ToBoolean(tableColumn["is_sparse"] ?? false);
                    currentColumn.NullBit = Convert.ToInt16(tableColumn["leaf_null_bit"]);

                    this.Columns.Add(currentColumn);

                    if (currentColumn.Sparse)
                    {
                        this.HasSparseColumns = true;
                    }
                }
            }
        }

        public override DataTable GetStructure(long allocationUnitId, Database database)
        {
            DataTable returnDataTable = new DataTable();

            using (SqlConnection conn = new SqlConnection(database.ConnectionString))
            {
                string commandText;

                if (database.CompatibilityLevel > 90)
                {
                    commandText = Properties.Resources.SQL_Table_Columns_2008;
                }
                else
                {
                    commandText = Properties.Resources.SQL_Table_Columns_2005;
                }

                SqlCommand cmd = new SqlCommand(commandText, conn);
                cmd.CommandType = CommandType.Text;

                cmd.Parameters.AddWithValue("@allocation_unit_id", allocationUnitId);
                cmd.CommandType = CommandType.Text;

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                conn.Open();

                if (conn.Database != database.Name)
                {
                    conn.ChangeDatabase(database.Name);
                }

                da.Fill(returnDataTable);

                conn.Close();
            }

            return returnDataTable;
        }

        public int ColumnCount
        {
            get { return columnCount; }
            set { columnCount = value; }
        }
    }
}
