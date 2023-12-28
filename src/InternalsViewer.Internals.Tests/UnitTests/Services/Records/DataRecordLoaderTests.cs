using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Services.Records.Loaders;
using InternalsViewer.Internals.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Records;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Records;

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

    public void Can_Parse_Variable_And_Fixed_Length_Not_Null_Record()
    {

    }

    public void Can_Parse_Variable_And_Fixed_Length_Null_Record()
    {

    }
}
