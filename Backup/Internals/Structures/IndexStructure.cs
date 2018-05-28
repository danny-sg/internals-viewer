using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;

namespace InternalsViewer.Internals.Structures
{
    public class IndexStructure : Structure
    {
        private bool heap;

        private byte indexType;
        private bool unique;

        public IndexStructure(long allocationUnitId, Database database)
            : base(allocationUnitId, database)
        {
            this.AddColumns(this.StructureDataTable);
        }

        internal override void AddColumns(DataTable structure)
        {
            IndexColumn currentColumn;

            if (structure.Rows.Count > 0)
            {
                foreach (DataRow indexColumn in structure.Rows)
                {
                    short offset;

                    if (Convert.ToInt32(indexColumn["index_id"]) == 1)
                    {
                        offset = Convert.ToInt16(indexColumn["internal_offset"]);
                    }
                    else
                    {
                        offset = Convert.ToInt16(indexColumn["leaf_offset"]);
                    }

                    currentColumn = new IndexColumn();

                    currentColumn.ColumnName = indexColumn["name"].ToString();
                    currentColumn.IndexColumnId = Convert.ToInt32(indexColumn["index_column_id"]);
                    currentColumn.ColumnId = Convert.ToInt16(indexColumn["internal_null_bit"]);
                    currentColumn.IncludedColumn = Convert.ToBoolean(indexColumn["is_included_column"]);
                    currentColumn.DataType = DataConverter.ToSqlType(Convert.ToByte(indexColumn["system_type_id"]));
                    currentColumn.DataLength = Convert.ToInt16(indexColumn["max_length"]);
                    currentColumn.LeafOffset = Convert.ToInt16(offset);
                    currentColumn.Key = Convert.ToBoolean(indexColumn["IsKey"]);
                    currentColumn.Uniqueifer = Convert.ToBoolean(indexColumn["is_uniqueifier"]);
                    currentColumn.Dropped = Convert.ToBoolean(indexColumn["is_dropped"]);

                    Columns.Add(currentColumn);

                    unique = Convert.ToBoolean(indexColumn["is_unique"]);
                    indexType = Convert.ToByte(indexColumn["type"]);
                    heap = Convert.ToInt32(indexColumn["hasClusteredIndex"]) != 1;
                }
            }
        }

        public override DataTable GetStructure(long allocationUnitId, Database database)
        {
            DataTable returnDataTable = new DataTable();

            using (SqlConnection conn = new SqlConnection(database.ConnectionString))
            {
                string commandText;

                commandText = Properties.Resources.SQL_Index_Columns;

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

        public bool Heap
        {
            get { return heap; }
            set { heap = value; }
        }

        public bool Unique
        {
            get { return unique; }
            set { unique = value; }
        }
    }
}
