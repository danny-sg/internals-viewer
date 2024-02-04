using InternalsViewer.Internals.Tests.Helpers;
using System.Data;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Loaders.Records;

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
///                      ,'NullBitIndex = ',            pc.leaf_null_bit,' }')
///         FROM   sys.allocation_units au
///                INNER JOIN sys.partitions p ON au.container_id = p.partition_id
///                INNER JOIN sys.system_internals_partition_columns pc ON p.partition_id = pc.partition_id
///                INNER JOIN sys.all_objects o ON p.object_id = o.object_id
///                LEFT JOIN sys.all_columns c ON column_id = partition_column_id AND c.object_id = p.object_id
///         WHERE  o.name = 'DataRecordLoaderTests_VariableFixed_NotNull'
/// </remarks>
public class FixedVarDataRecordLoaderTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutput { get; set; } = testOutputHelper;

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
    /// DBCC Page Output:
    /// 
    ///     Slot 0 Offset 0x60 Length 29
    ///     
    ///     Record Type = PRIMARY_RECORD        Record Attributes =  NULL_BITMAP    Record Size = 29
    ///     
    ///     Memory Dump @0x00000051283F6060
    ///     
    ///     0000000000000000:   10001a00 7b000000 f4a59385 0f000000 54657374  ....{...ô¥....Test
    ///     0000000000000014:   20436f6c 20330300 00                           Col 3...
    ///     
    ///     Slot 0 Column 1 Offset 0x4 Length 4 Length (physical) 4
    ///     
    ///     Column1 = 123                       
    ///     
    ///     Slot 0 Column 2 Offset 0x8 Length 8 Length (physical) 8
    ///     
    ///     Column2 = 66665555444               
    ///     
    ///     Slot 0 Column 3 Offset 0x10 Length 10 Length (physical) 10
    ///     
    ///     Column3 = Test Col 3     
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
            NullBitIndex = 1
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column2",
            ColumnId = 2,
            DataType = SqlDbType.BigInt,
            DataLength = 8,
            LeafOffset = 8,
            Precision = 19,
            NullBitIndex = 2
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column3",
            ColumnId = 3,
            DataType = SqlDbType.Char,
            DataLength = 10,
            LeafOffset = 16,
            Precision = 0,
            NullBitIndex = 3
        });

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var record = loader.Load(new DataPage { Data = data }, 0, structure);

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
    /// 
    /// DBCC Page output:
    /// 
    ///     Record Type = PRIMARY_RECORD        Record Attributes =  NULL_BITMAP VARIABLE_COLUMNS
    ///     Record Size = 54                    
    ///     Memory Dump @0x00000051283F6060
    ///     
    ///     0000000000000000:   30000400 02000002 00140036 0056616c 75652031  0..........6.Value 1
    ///     0000000000000014:   44006900 66006600 65007200 65006e00 74002000  D.i.f.f.e.r.e.n.t. .
    ///     0000000000000028:   56006100 6c007500 65002000 3200               V.a.l.u.e. .2.
    ///     
    ///     Slot 0 Column 1 Offset 0xd Length 7 Length (physical) 7
    ///     
    ///     Column1 = Value 1                   
    ///     
    ///     Slot 0 Column 2 Offset 0x14 Length 34 Length (physical) 34
    ///     
    ///     Column2 = Different Value 2      
    /// </remarks>
    [Fact]
    public void Can_Parse_Variable_Length_Only_Record()
    {
        var rowValue = "30 00 04 00 02 00 00 02 00 14 00 36 00 56 61 6C 75 65 20 31 " +
                       "44 00 69 00 66 00 66 00 65 00 72 00 65 00 6E 00 74 00 20 00 " +
                       "56 00 61 00 6C 00 75 00 65 00 20 00 32 00";

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
            NullBitIndex = 1
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column2",
            ColumnId = 2,
            DataType = SqlDbType.NVarChar,
            DataLength = 100,
            LeafOffset = -2,
            Precision = 0,
            NullBitIndex = 2
        });

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var record = loader.Load(new DataPage { Data = data }, 0, structure);

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
    /// 
    /// DBCC Page output:
    /// 
    ///     Slot 0 Offset 0x60 Length 57
    ///     
    ///     Record Type = PRIMARY_RECORD        Record Attributes =  NULL_BITMAP VARIABLE_COLUMNS
    ///     Record Size = 57                    
    ///     Memory Dump @0x000000512C176060
    ///     
    ///     0000000000000000:   30001200 01000000 41424320 20202020 20200400  0.......ABC       ..
    ///     0000000000000014:   00020025 00390056 61726961 626c6520 41560061  ...%.9.Variable AV.a
    ///     0000000000000028:   00720069 00610062 006c0065 00200042 00        .r.i.a.b.l.e. .B.
    ///     
    ///     Slot 0 Column 1 Offset 0x4 Length 4 Length (physical) 4
    ///     
    ///     Column1 = 1                         
    ///     
    ///     Slot 0 Column 2 Offset 0x8 Length 10 Length (physical) 10
    ///     
    ///     Column2 = ABC                       
    ///     
    ///     Slot 0 Column 3 Offset 0x1b Length 10 Length (physical) 10
    ///     
    ///     Column3 = Variable A                
    ///     
    ///     Slot 0 Column 4 Offset 0x25 Length 20 Length (physical) 20
    ///     
    ///     Column4 = Variable B   
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
            NullBitIndex = 1
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column2",
            ColumnId = 2,
            DataType = SqlDbType.Char,
            DataLength = 10,
            LeafOffset = 8,
            Precision = 19,
            NullBitIndex = 2
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column3",
            ColumnId = 3,
            DataType = SqlDbType.VarChar,
            DataLength = 100,
            LeafOffset = -1,
            Precision = 0,
            NullBitIndex = 3
        });

        structure.Columns.Add(new ColumnStructure
        {
            ColumnName = "Column4",
            ColumnId = 4,
            DataType = SqlDbType.NVarChar,
            DataLength = 50,
            LeafOffset = -2,
            Precision = 0,
            NullBitIndex = 4
        });

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var record = loader.Load(new DataPage { Data = data }, 0, structure);

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

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var record = loader.Load(new DataPage { Data = data }, 0, structure);

        Assert.Equal(RecordType.ForwardingStub, record.RecordType);
        Assert.Equal(new RowIdentifier(1, 19602, 0), record.ForwardingStub);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// DBCC Page output:
    /// 
    ///     Record Type = PRIMARY_RECORD        Record Attributes =  NULL_BITMAP VARIABLE_COLUMNS
    ///     Record Size = 91                    
    ///     Memory Dump @0x0000005123FF6060
    ///     
    ///     0000000000000000:   30000800 01000000 04000003 002b8043 805b8002  0............+.C.[..
    ///     0000000000000014:   00000001 00000084 67000040 1f00004f 03000001  .......g..@...O....
    ///     0000000000000028:   00000002 00000001 000000e1 4a000040 1f000068  ...........áJ..@...h
    ///     000000000000003C:   03000001 00000004 00000001 00000029 00000040  ...............)...@
    ///     0000000000000050:   1f000058 03000001 000000                      ...X.......
    ///     
    ///     Slot 0 Column 1 Offset 0x4 Length 4 Length (physical) 4
    ///     
    ///     Column1 = 1                         
    ///     
    ///     Column2 = [BLOB Inline Root] Slot 0 Column 2 Offset 0x13 Length 24 Length (physical) 24
    ///     
    ///     Level = 0                           Unused = 0                          UpdateSeq = 1
    ///     TimeStamp = 1736704000              Type = 2                            
    ///     Link 0
    ///     
    ///     Size = 8000                         RowId = (1:847:0)                   
    ///     
    ///     Column3 = [BLOB Inline Root] Slot 0 Column 3 Offset 0x2b Length 24 Length (physical) 24
    ///     
    ///     Level = 0                           Unused = 0                          UpdateSeq = 1
    ///     TimeStamp = 1256259584              Type = 2                            
    ///     Link 0
    ///     
    ///     Size = 8000                         RowId = (1:872:0)                   
    ///     
    ///     Column4 = [BLOB Inline Root] Slot 0 Column 4 Offset 0x43 Length 24 Length (physical) 24
    ///     
    ///     Level = 0                           Unused = 0                          UpdateSeq = 1
    ///     TimeStamp = 2686976                 Type = 4                            
    ///     Link 0
    ///     
    ///     Size = 8000                         RowId = (1:856:0) 
    /// </remarks>
    [Fact]
    public void Can_Parse_Lob_Records()
    {
        var rowValue = "30 00 08 00 01 00 00 00 04 00 00 03 00 2B 80 43 " +
                       "80 5B 80 02 00 00 00 01 00 00 00 84 67 00 00 40 " +
                       "1F 00 00 4F 03 00 00 01 00 00 00 02 00 00 00 01 " +
                       "00 00 00 E1 4A 00 00 40 1F 00 00 68 03 00 00 01 " +
                       "00 00 00 04 00 00 00 01 00 00 00 29 00 00 00 40 " +
                       "1F 00 00 58 03 00 00 01 00 00 00";

        var data = rowValue.ToByteArray();

        var structure = new TableStructure(100);

        structure.Columns.Add(new ColumnStructure { ColumnName = "Column1", ColumnId = 1, DataType = SqlDbType.Int, LeafOffset = 4, Precision = 10, NullBitIndex = 1 });
        structure.Columns.Add(new ColumnStructure { ColumnName = "Column2", ColumnId = 2, DataType = SqlDbType.VarChar, LeafOffset = -1, Precision = 0, NullBitIndex = 2 });
        structure.Columns.Add(new ColumnStructure { ColumnName = "Column3", ColumnId = 3, DataType = SqlDbType.VarChar, LeafOffset = -2, Precision = 0, NullBitIndex = 3 });
        structure.Columns.Add(new ColumnStructure { ColumnName = "Column4", ColumnId = 4, DataType = SqlDbType.VarChar, LeafOffset = -3, Precision = 0, NullBitIndex = 4 });

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var record = loader.Load(new DataPage { Data = data }, 0, structure);
    }
}
