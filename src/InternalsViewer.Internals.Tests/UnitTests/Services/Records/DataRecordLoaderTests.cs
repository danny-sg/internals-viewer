using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Services.Records.Loaders;
using InternalsViewer.Internals.Tests.Helpers;
using System.Data;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Records;

/// <summary>
/// Data Record Loader Tests
/// </summary>
/// <remarks>
/// SQL to generate structure:
/// 
///         SELECT CONCAT('{ '
///                      ,'ColumnName = "',        c.name, '", '
///                      ,'ColumnId = ',           c.column_id,', '
///                      ,'DataType = SqlDbType.', TYPE_NAME(pc.system_type_id),', '
///                      ,'LeafOffset = ',         pc.leaf_offset,', '
///                      ,'Precision = ',          c.precision,', '
///                      ,'NullBit = ',            pc.leaf_null_bit,' }')
///         FROM   sys.allocation_units au
///                INNER JOIN sys.partitions p ON au.container_id = p.partition_id
///                INNER JOIN sys.system_internals_partition_columns pc ON p.partition_id = pc.partition_id
///                INNER JOIN sys.all_objects o ON p.object_id = o.object_id
///                LEFT JOIN sys.all_columns c ON column_id = partition_column_id AND c.object_id = p.object_id
///         WHERE  o.name = 'DataRecordLoaderTests_VariableFixed_NotNull'
/// </remarks>
public class DataRecordLoaderTests
{
    /// <summary>
    /// Parse a fixed length only record
    /// </summary>
    /// <remarks>
    /// CREATE TABLE Testing.DataRecordLoaderTests_FixedLengthOnly
    /// (
    ///     Column1 INT      NOT NULL
    ///    ,Column2 BIGINT   NOT NULL
    ///    ,Column3 CHAR(10) NOT NULL
    /// )
    /// 
    /// INSERT INTO Testing.DataRecordLoaderTests VALUES (123, 66665555444, 'Test Col 3')
    /// GO
    /// 
    /// </remarks>
    [Fact]
    public void Can_Parse_Fixed_Length_Only_Record()
    {
        var rowValue = "10 00 1A 00 7B 00 00 00 F4 A5 93 85 0F 00 00 00 54 65 73 74 20 43 6F 6C 20 33 03 00 00";

        var data = rowValue.ToByteArray();

        var structure = new TableStructure(100);

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column1",
            ColumnId = 1,
            DataType = SqlDbType.Int,
            DataLength = 4,
            LeafOffset = 4,
            Precision = 10,
            NullBit = 1
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column2",
            ColumnId = 2,
            DataType = SqlDbType.BigInt,
            DataLength = 8,
            LeafOffset = 8,
            Precision = 19,
            NullBit = 2
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column3",
            ColumnId = 3,
            DataType = SqlDbType.Char,
            DataLength = 10,
            LeafOffset = 16,
            Precision = 0,
            NullBit = 3
        });

        var record = DataRecordLoader.Load(data, 0, structure);

        Assert.Equal(RecordType.Primary, record.RecordType);
        Assert.Equal(false, record.HasVariableLengthColumns);

        Assert.Equal(123, record.GetValue<int>("Column1"));
        Assert.Equal(66665555444, record.GetValue<long>("Column2"));
        Assert.Equal("Test Col 3", record.GetValue<string>("Column3"));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// CREATE TABLE Testing.DataRecordLoaderTests_VariableLengthOnly
    /// (
    ///     Column1 VARCHAR(100) NOT NULL
    ///    ,Column2 NVARCHAR(50) NOT NULL
    /// )
    /// 
    /// INSERT INTO Testing.DataRecordLoaderTests_VariableLengthOnly VALUES ('Value 1', 'Different Value 2')
    /// </remarks>
    [Fact]
    public void Can_Parse_Variable_Length_Only_Record()
    {
        var rowValue = "30 00 04 00 02 00 00 02 00 14 00 36 00 56 61 6C 75 65 20 31 44 00 69 00 66 00 66 00 65 00 72 00 65 00 6E 00 74 "
                      + "00 20 00 56 00 61 00 6C 00 75 00 65 00 20 00 32 00";
        
        var data = rowValue.ToByteArray();

        var structure = new TableStructure(100);

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column1",
            ColumnId = 1,
            DataType = SqlDbType.VarChar,
            DataLength = 100,
            LeafOffset = -1,
            Precision = 0,
            NullBit = 1
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column2",
            ColumnId = 2,
            DataType = SqlDbType.NVarChar,
            DataLength = 100,
            LeafOffset = -2,
            Precision = 0,
            NullBit = 2
        });

        var record = DataRecordLoader.Load(data, 0, structure);

        Assert.Equal(RecordType.Primary, record.RecordType);
        Assert.Equal(true, record.HasVariableLengthColumns);

        Assert.Equal("Value 1", record.GetValue<string>("Column1"));
        Assert.Equal("Different Value 2", record.GetValue<string>("Column2"));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// CREATE TABLE Testing.DataRecordLoaderTests_VariableFixed_NotNull
    /// (
    ///     Column1 INT          NOT NULL
    ///    ,Column2 CHAR(10)     NOT NULL
    ///    ,Column3 VARCHAR(100) NOT NULL
    ///    ,Column4 NVARCHAR(50) NOT NULL
    /// )
    /// 
    /// INSERT INTO Testing.DataRecordLoaderTests_VariableFixed_NotNull VALUES (1, 'ABC', 'Variable A', 'Variable B')
    /// </remarks>
    [Fact]
    public void Can_Parse_Variable_And_Fixed_Length_Not_Null_Record()
    {
        var rowValue = "30 00 12 00 01 00 00 00 41 42 43 20 20 20 20 20 "
                     + "20 20 04 00 00 02 00 25 00 39 00 56 61 72 69 61 "
                     + "62 6C 65 20 41 56 00 61 00 72 00 69 00 61 00 62 "
                     + "00 6C 00 65 00 20 00 42 00 ";

        var data = rowValue.ToByteArray();

        var structure = new TableStructure(100);

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column1",
            ColumnId = 1,
            DataType = SqlDbType.Int,
            DataLength = 4,
            LeafOffset = 4,
            Precision = 10,
            NullBit = 1
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column2",
            ColumnId = 2,
            DataType = SqlDbType.Char,
            DataLength = 10,
            LeafOffset = 8,
            Precision = 19,
            NullBit = 2
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column3",
            ColumnId = 3,
            DataType = SqlDbType.VarChar,
            DataLength = 100,
            LeafOffset = -1,
            Precision = 0,
            NullBit = 3
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column4",
            ColumnId = 4,
            DataType = SqlDbType.NVarChar,
            DataLength = 50,
            LeafOffset = -2,
            Precision = 0,
            NullBit = 4
        });

        var record = DataRecordLoader.Load(data, 0, structure);

        Assert.Equal(RecordType.Primary, record.RecordType);
        Assert.Equal(true, record.HasVariableLengthColumns);

        Assert.Equal(1, record.GetValue<int>("Column1"));
        Assert.Equal("ABC       ", record.GetValue<string>("Column2"));
        Assert.Equal("Variable A", record.GetValue<string>("Column3"));
        Assert.Equal("Variable B", record.GetValue<string>("Column4"));
    }

    public void Can_Parse_Variable_And_Fixed_Length_Null_Record()
    {

    }

    [Fact]
    public void Can_Parse_Forwarding_Stub()
    {
        var rowValue = "04 92 4C 00 00 01 00";

        var data = rowValue.ToByteArray();

        var structure = new TableStructure(100);

        var record = DataRecordLoader.Load(data, 0, structure);

        Assert.Equal(RecordType.ForwardingStub, record.RecordType);
        Assert.Equal(new RowIdentifier(1, 19602, 0), record.ForwardingStub);
    }
}
