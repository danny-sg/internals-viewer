using System.Collections.Generic;
using System.Data;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// SQL Server Allocation Units definitions table - sys.sysallocunits    
/// </summary>
public record InternalAllocationUnit
{
    public long Auid { get; set; }

    public byte Type { get; set; }

    public long OwnerId { get; set; }

    public int Status { get; set; }

    public short Fgid { get; set; }

    public byte[] Pgfirst { get; set; }

    public byte[] Pgroot { get; set; }

    public byte[] Pgfirstiam { get; set; }

    public long Pcused { get; set; }

    public long Pcdata { get; set; }

    public long Pcreserved { get; set; }

    public static AllocationUnit GetAllocationUnit()
    {
        // 1:20
        const long allocationUnitId = 458752;

        var structure = new TableStructure(allocationUnitId);

        var columns = new List<ColumnStructure>
        {
            new() { ColumnName = "auid", ColumnId = 1, DataType = SqlDbType.BigInt, DataLength = 8, LeafOffset = 4, NullBit = 1 },
            new() { ColumnName = "type", ColumnId = 2, DataType = SqlDbType.TinyInt, DataLength = 1, LeafOffset = 12, NullBit = 2 },
            new() { ColumnName = "ownerid", ColumnId = 3, DataType = SqlDbType.BigInt, DataLength = 8, LeafOffset = 13, NullBit = 3 },
            new() { ColumnName = "status", ColumnId = 4, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 21, NullBit = 4 },
            new() { ColumnName = "fgid", ColumnId = 5, DataType = SqlDbType.SmallInt, DataLength = 2, LeafOffset = 25, NullBit = 5 },
            new() { ColumnName = "pgfirst", ColumnId = 6, DataType = SqlDbType.Binary, DataLength = 6, LeafOffset = 27, NullBit = 6 },
            new() { ColumnName = "pgroot", ColumnId = 7, DataType = SqlDbType.Binary, DataLength = 6, LeafOffset = 33, NullBit = 7 },
            new() { ColumnName = "pgfirstiam", ColumnId = 8, DataType = SqlDbType.Binary, DataLength = 6, LeafOffset = 39, NullBit = 8 },
            new() { ColumnName = "pcused", ColumnId = 9, DataType = SqlDbType.BigInt, DataLength = 8, LeafOffset = 45, NullBit = 9 },
            new() { ColumnName = "pcdata", ColumnId = 10, DataType = SqlDbType.BigInt, DataLength = 8, LeafOffset = 53, NullBit = 10 },
            new() { ColumnName = "pcreserved", ColumnId = 11, DataType = SqlDbType.BigInt, DataLength = 8, LeafOffset = 61, NullBit = 11 }
        };

        structure.Columns = columns;

        var allocationUnit = new AllocationUnit
        {
            AllocationUnitId = allocationUnitId,
            TableName = "sys.sysallocunits",
            ObjectId = 7,
            IndexId = 0
        };

        return allocationUnit;
    }
}