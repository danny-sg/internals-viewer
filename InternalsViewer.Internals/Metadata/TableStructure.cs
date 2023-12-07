using System;
using System.Data;
using InternalsViewer.Internals.Engine.Database;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Metadata;

public class TableStructure : Structure
{
    public TableStructure(long allocationUnitId, Database database)
        : base(allocationUnitId, database)
    {
        AddColumns(StructureDataTable);
    }

    internal override void AddColumns(DataTable structure)
    {
        if (structure.Rows.Count > 0)
        {
            foreach (DataRow tableColumn in structure.Rows)
            {
                ColumnCount++;

                var currentColumn = new Column();

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

                Columns.Add(currentColumn);

                if (currentColumn.Sparse)
                {
                    HasSparseColumns = true;
                }
            }
        }
    }

    public override DataTable GetStructure(long allocationUnitId, Database database)
    {
        var returnDataTable = new DataTable();

        using var conn = new SqlConnection(database.ConnectionString);

        string commandText;

        commandText = SqlCommands.TableColumns;

        var cmd = new SqlCommand(commandText, conn);

        cmd.CommandType = CommandType.Text;

        cmd.Parameters.AddWithValue("@AllocationUnitId", allocationUnitId);
        cmd.CommandType = CommandType.Text;

        var da = new SqlDataAdapter(cmd);

        conn.Open();

        if (conn.Database != database.Name)
        {
            conn.ChangeDatabase(database.Name);
        }

        da.Fill(returnDataTable);

        conn.Close();

        return returnDataTable;
    }

    public int ColumnCount { get; set; }
}