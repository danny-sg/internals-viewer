using System;
using System.Collections.Generic;
using System.Data;

// ReSharper disable IdentifierTypo

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// SQL Server Column definitions table - sys.sysrscols
/// </summary>
/// <remarks>
/// Table only accessible via DAC connection (prefix connection with admin:)
/// 
/// This table is needed to understand the structure of the row
/// 
/// Relevant fields are:
/// 
///     Leaf Offset   - offset & 0xffff
///     Is Uniquifier - status & 16
///     Is Dropped    - status & 2
///     Is Sparse     - status & 0x00000100
///     /// </remarks>
internal record InternalColumn
{
    public long Rsid { get; set; }

    public int Rscolid { get; set; }

    public int Bcolid { get; set; }

    public int Rcmodified { get; set; }

    public int Ti { get; set; }

    public int Cid { get; set; }

    public short Ordkey { get; set; }

    public short Maxrowinlen { get; set; }

    public int Status { get; set; }

    public int Offset { get; set; }

    public int Nullbit { get; set; }

    public short Bitpos { get; set; }

    public byte[] Colguid { get; set; } = Array.Empty<byte>();

    public int Ordlock { get; set; }

    public static TableStructure GetTableStructure()
    {
        var structure = new TableStructure(196608);

        var columns = new List<ColumnStructure>
        {
            new() { ColumnName = "rsid", ColumnId = 1, DataType = SqlDbType.BigInt, DataLength = 8, LeafOffset = 4, NullBit = 1 },
            new() { ColumnName = "rscolid", ColumnId = 2, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 12, NullBit = 2 },
            new() { ColumnName = "hbcolid", ColumnId = 3, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 16, NullBit = 3 },
            new() { ColumnName = "rcmodified", ColumnId = 4, DataType = SqlDbType.BigInt, DataLength = 8, LeafOffset = 20, NullBit = 4 },
            new() { ColumnName = "ti", ColumnId = 5, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 28, NullBit = 5 },
            new() { ColumnName = "cid", ColumnId = 6, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 32, NullBit = 6 },
            new() { ColumnName = "ordkey", ColumnId = 7, DataType = SqlDbType.SmallInt, DataLength = 2, LeafOffset = 36, NullBit = 7 },
            new() { ColumnName = "maxinrowlen", ColumnId = 8, DataType = SqlDbType.SmallInt, DataLength = 2, LeafOffset = 38, NullBit = 8 },
            new() { ColumnName = "status", ColumnId = 9, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 40, NullBit = 9 },
            new() { ColumnName = "offset", ColumnId = 10, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 44, NullBit = 10 },
            new() { ColumnName = "nullbit", ColumnId = 11, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 48, NullBit = 11 },
            new() { ColumnName = "bitpos", ColumnId = 12, DataType = SqlDbType.SmallInt, DataLength = 4, LeafOffset = 52, NullBit = 12 },
            new() { ColumnName = "colguid", ColumnId = 13, DataType = SqlDbType.VarBinary, DataLength = 16, LeafOffset = -1, NullBit = 13 },
            new() { ColumnName = "ordlock", ColumnId = 14, DataType = SqlDbType.Int, DataLength = 4, LeafOffset = 54, NullBit = 14 }
        };

        structure.Columns = columns;

        return structure;
    }
}