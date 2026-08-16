using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Memory;

/// <summary>
/// Sizes the rows and structures a buffering operator holds, which is a model of SQL Server's workspace rather than a measurement of it
/// </summary>
/// <remarks>
/// Fitted against the grants SQL Server 2025 reported for sorts and hash matches over the demo database, one variable moved at a time:
///
///     Sort, used memory per row against the plan's AvgRowSize (20,000 rows)
///
///         AvgRowSize  21    61    111   211   411
///         Bytes/row   61.0  102.0 152.8 256.4 471.9      => 1.03 x AvgRowSize + 39.5
///
///     Sort, bytes per row by row count at AvgRowSize 61
///
///         Rows        1,000 5,000 20,000 50,000 100,000
///         Bytes/row   106.5 103.2 102.0  101.6  101.4    => the same slope once the floor washes out
///
///     Hash match, build side of 20,000 rows: 1.05 x AvgRowSize + ~51, over a floor of roughly a quarter of a megabyte
///
/// Two findings shape the model. Five CHAR(10) columns and one CHAR(50) used exactly the same memory, so a fixed length column costs
/// nothing beyond its data, while variable length columns cost about two bytes each for the offset the row keeps. And the per row cost
/// beyond the row image is the same for both operators, so it belongs to the workspace rather than to the sort or the table.
///
/// What is left over is granularity. A grant is whole 8KB pages, so a small operator is nearly all floor, and the last few percent of a
/// large one is the packing waste of rows that do not divide into a page.
/// </remarks>
public static class RowMemory
{
    /// <summary>
    /// Status bits, the offset the record keeps to the end of its fixed length data, and the column count
    /// </summary>
    /// <remarks>
    /// The parts of the record format that every row carries, used only where a record cannot say its own length. A decoded record is
    /// measured rather than modelled, which is what makes the row half of this exact.
    /// </remarks>
    public const int RowHeaderBytes = 6;

    /// <summary>
    /// Cost of a variable length column, which is the offset the record keeps to the end of its data
    /// </summary>
    /// <remarks>
    /// Fixed length columns pay nothing here, measured rather than assumed: a row of five CHAR(10) columns took the same memory as a row
    /// of one CHAR(50).
    /// </remarks>
    public const int VariableColumnBytes = 2;

    /// <summary>
    /// The count a record keeps of its variable length columns, which it has only where it has any
    /// </summary>
    public const int VariableCountBytes = 2;

    /// <summary>
    /// Cost of holding one row in a workspace, beyond the row itself
    /// </summary>
    /// <remarks>
    /// The same for a sort run and a hash table, which is why it is one constant. Whatever it is made of, an operator pays it per row it
    /// is holding rather than per row it has read.
    /// </remarks>
    public const int WorkspaceRowBytes = 40;

    /// <summary>
    /// The least a sort takes, which is the page or so it holds before any row reaches it
    /// </summary>
    public const int SortFloorBytes = 8 * 1024;

    /// <summary>
    /// The least a hash match takes, its table structure before any row is hashed into it
    /// </summary>
    /// <remarks>
    /// A hash match spreads its rows over partitions that each hold whole pages, so a table holding few rows still holds a great many
    /// part filled ones. Build sides of 1,000 to 100,000 narrow rows used 352, 768, 1,216, 2,584 and 5,080KB, where the rows themselves
    /// account for 51, 254, 1,016, 2,539 and 5,078KB: the waste is everything at the small end and nothing at the large one. This is the
    /// middle of that range rather than a measurement of the structure, so a small hash match is the least exact figure the model gives.
    /// </remarks>
    public const int HashFloorBytes = 384 * 1024;

    /// <summary>
    /// The page a workspace is allocated in, which is what makes a grant a whole number of pages
    /// </summary>
    public const int PageBytes = 8 * 1024;

    /// <summary>
    /// The size of one row once copied into a buffer
    /// </summary>
    /// <remarks>
    /// A record that was decoded off a page knows what it took up there, which is the figure to use: the plan's AvgRowSize for a row of
    /// an INT and a CHAR(50) is 61, and so is the record's length. Only a row assembled rather than read - a projection, a row of a
    /// format that does not carry its length - falls back to being counted a column at a time.
    /// </remarks>
    public static long SizeOf(IRecord record)
        => record is FixedVarRecord fixedVar ? fixedVar.RecordLength : SizeOfFields(record);

    /// <summary>
    /// The size of a row of the given column widths, which is the same model reached without a record to read
    /// </summary>
    public static long SizeOf(IEnumerable<int> columnWidths, int variableColumns = 0)
    {
        long size = RowHeaderBytes;

        var columns = 0;

        foreach (var width in columnWidths)
        {
            size += width;

            columns++;
        }

        return size + NullBitmapBytes(columns) + VariableArrayBytes(variableColumns);
    }

    /// <summary>
    /// What a sort holding the given rows takes
    /// </summary>
    public static BufferMemory ForSort(long rowBytes, long rows)
        => new(rowBytes, (rows * WorkspaceRowBytes) + SortFloorBytes);

    /// <summary>
    /// What a hash table holding the given rows takes
    /// </summary>
    public static BufferMemory ForHashTable(long rowBytes, long rows)
        => new(rowBytes, (rows * WorkspaceRowBytes) + HashFloorBytes);

    private static long SizeOfFields(IRecord record)
    {
        var size = RowHeaderBytes + NullBitmapBytes(record.Fields.Count);

        var variableColumns = 0;

        foreach (var field in record.Fields)
        {
            size += field.Length;

            if (field.ColumnStructure.LeafOffset < 0)
            {
                variableColumns++;
            }
        }

        return size + VariableArrayBytes(variableColumns);
    }

    private static long NullBitmapBytes(int columns) => (columns + 7) / 8;

    private static long VariableArrayBytes(int variableColumns)
        => variableColumns == 0 ? 0 : VariableCountBytes + ((long)variableColumns * VariableColumnBytes);
}
