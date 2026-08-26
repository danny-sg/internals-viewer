using System.Data;
using System.Data.SqlTypes;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

public class BatchSlotRoundTripTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(4611686018427387903)]
    [InlineData(-4611686018427387904)]
    public void Integer_Round_Trips_While_It_Fits_In_Sixty_Three_Bits(long value)
    {
        Assert.True(BatchSlotNormalizer.TryNormalize(value, out var slot));

        Assert.Equal(value, BatchSlotDenormalizer.GetStorageValue(slot, Column(SqlDbType.BigInt)));
    }

    [Theory]
    [InlineData(4611686018427387904)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Integer_Does_Not_Normalize_Once_The_Doubling_Overflows(long value)
    {
        Assert.False(BatchSlotNormalizer.TryNormalize(value, out _));
    }

    [Fact]
    public void Integer_Leaves_The_Tag_Clear_So_The_Slot_Reads_As_A_Value()
    {
        BatchSlotNormalizer.TryNormalize(42, out var slot);

        Assert.False(slot.IsDeepDataReference);

        Assert.False(slot.IsNull);

        Assert.Equal(BatchSlotValueType.Inline, BatchSlotDenormalizer.GetValueType(slot, Column(SqlDbType.BigInt)));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-1d)]
    [InlineData(3.14159d)]
    [InlineData(-2.5d)]
    [InlineData(1e300d)]
    [InlineData(-1e300d)]
    [InlineData(1e-300d)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    public void Real_Round_Trips_While_The_Two_Exponent_Bits_Match(double value)
    {
        Assert.True(BatchSlotNormalizer.TryNormalize(value, out var slot));

        var storage = BatchSlotDenormalizer.GetStorageValue(slot, Column(SqlDbType.Float));

        Assert.Equal(value, BitConverter.Int64BitsToDouble(storage));
    }

    /// <summary>
    /// The bands that go deep are the middling magnitudes, ordinary and extreme values both staying inline
    /// </summary>
    [Theory]
    [InlineData(1e100d)]
    [InlineData(-1e100d)]
    [InlineData(1e-100d)]
    [InlineData(1e200d)]
    [InlineData(1e-200d)]
    public void Real_Does_Not_Normalize_When_The_Two_Exponent_Bits_Differ(double value)
    {
        Assert.False(BatchSlotNormalizer.TryNormalize(value, out _));
    }

    [Theory]
    [InlineData(12345, 5, 2)]
    [InlineData(-12345, 5, 2)]
    [InlineData(0, 18, 0)]
    [InlineData(999999999999999999, 18, 0)]
    public void Numeric_Round_Trips_Through_The_Same_Doubling_As_An_Integer(long scaled, byte precision, byte scale)
    {
        var magnitude = (ulong)Math.Abs(scaled);

        var value = new SqlDecimal(precision, scale, scaled >= 0, (int)(magnitude & 0xFFFFFFFF), (int)(magnitude >> 32), 0, 0);

        Assert.True(BatchSlotNormalizer.TryNormalize(value, out var slot));

        Assert.Equal(scaled, BatchSlotDenormalizer.GetStorageValue(slot, Column(SqlDbType.Decimal, precision, scale)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(630822816000000000)]
    [InlineData(3155378975999999999)]
    public void DateTime2_Round_Trips_As_A_Tick_Count_At_The_Maximum_Scale(long ticks)
    {
        var value = new DateTime(ticks);

        Assert.True(BatchSlotNormalizer.TryNormalize(value, out var slot));

        Assert.Equal(value, BatchSlotDenormalizer.GetTemporalValue(slot, Column(SqlDbType.DateTime2)));
    }

    [Fact]
    public void Time_Round_Trips_As_A_Tick_Count()
    {
        var value = new TimeSpan(0, 13, 45, 30, 123);

        Assert.True(BatchSlotNormalizer.TryNormalize(value, out var slot));

        Assert.Equal(value, BatchSlotDenormalizer.GetTemporalValue(slot, Column(SqlDbType.Time)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(630822816000000000, 0)]
    [InlineData(630822816000000000, -300)]
    [InlineData(630822816000000000, 840)]
    [InlineData(3155378975999900000, -840)]
    public void DateTimeOffset_Round_Trips_When_It_Fits_At_Scale_Two(long utcTicks, int offsetMinutes)
    {
        var offset = TimeSpan.FromMinutes(offsetMinutes);

        var value = new DateTimeOffset(new DateTime(utcTicks, DateTimeKind.Utc)).ToOffset(offset);

        Assert.True(BatchSlotNormalizer.TryNormalize(value, out var slot));

        var read = (DateTimeOffset)BatchSlotDenormalizer.GetTemporalValue(slot, Column(SqlDbType.DateTimeOffset));

        Assert.Equal(value.UtcTicks, read.UtcTicks);

        Assert.Equal(value.Offset, read.Offset);
    }

    [Fact]
    public void DateTimeOffset_Does_Not_Normalize_Below_Scale_Two()
    {
        var value = new DateTimeOffset(new DateTime(630822816000000001, DateTimeKind.Utc));

        Assert.False(BatchSlotNormalizer.TryNormalize(value, out _));
    }

    [Theory]
    [InlineData(SqlDbType.UniqueIdentifier, BatchSlotDomain.Deep)]
    [InlineData(SqlDbType.VarBinary, BatchSlotDomain.Deep)]
    [InlineData(SqlDbType.Timestamp, BatchSlotDomain.Deep)]
    [InlineData(SqlDbType.Xml, BatchSlotDomain.Deep)]
    [InlineData(SqlDbType.VarChar, BatchSlotDomain.Dictionary)]
    [InlineData(SqlDbType.NVarChar, BatchSlotDomain.Dictionary)]
    public void Types_That_Never_Normalize_Are_Deep_Unless_They_Can_Use_A_Dictionary(SqlDbType dataType, BatchSlotDomain domain)
    {
        Assert.Equal(domain, Column(dataType).Domain);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(65535)]
    [InlineData(16777216)]
    public void Dictionary_Reference_Round_Trips_Its_Data_Id(long dataId)
    {
        var slot = BatchSlotNormalizer.FromDictionaryDataId(dataId);

        Assert.Equal(dataId, BatchSlotDenormalizer.GetDictionaryDataId(slot));

        Assert.Equal(BatchSlotValueType.DictionaryReference,
                     BatchSlotDenormalizer.GetValueType(slot, Column(SqlDbType.VarChar)));
    }

    [Fact]
    public void Null_Is_The_Flag_With_No_Value_And_Is_Not_Read_As_Deep()
    {
        var slot = BatchSlotNormalizer.Null;

        Assert.True(slot.IsNull);

        Assert.False(slot.IsDeepDataReference);

        Assert.Equal(BatchSlotValueType.Null, BatchSlotDenormalizer.GetValueType(slot, Column(SqlDbType.BigInt)));
    }

    [Theory]
    [InlineData(SqlDbType.BigInt, BatchSlotDomain.Integer)]
    [InlineData(SqlDbType.Bit, BatchSlotDomain.Integer)]
    [InlineData(SqlDbType.Money, BatchSlotDomain.Integer)]
    [InlineData(SqlDbType.Float, BatchSlotDomain.Real)]
    [InlineData(SqlDbType.Real, BatchSlotDomain.Real)]
    [InlineData(SqlDbType.Decimal, BatchSlotDomain.Numeric)]
    [InlineData(SqlDbType.DateTime2, BatchSlotDomain.Temporal)]
    [InlineData(SqlDbType.Time, BatchSlotDomain.Temporal)]
    [InlineData(SqlDbType.VarChar, BatchSlotDomain.Dictionary)]
    [InlineData(SqlDbType.NVarChar, BatchSlotDomain.Dictionary)]
    [InlineData(SqlDbType.VarBinary, BatchSlotDomain.Deep)]
    public void Domain_Follows_The_Data_Type(SqlDbType dataType, BatchSlotDomain domain)
    {
        Assert.Equal(domain, Column(dataType).Domain);
    }

    [Fact]
    public void Data_Ids_Are_Comparable_Only_Within_One_Space()
    {
        var scan = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(4, 2, 7) };

        var sameScan = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(4, 2, 7) };

        var otherScan = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(9, 2, 7) };

        var otherColumn = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(4, 3, 7) };

        Assert.True(scan.SharesDataIdsWith(sameScan));

        Assert.False(scan.SharesDataIdsWith(otherScan));

        Assert.False(scan.SharesDataIdsWith(otherColumn));
    }

    [Fact]
    public void Local_Data_Ids_Do_Not_Reach_Across_Row_Groups()
    {
        var first = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(4, 2, 7) };

        var second = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(4, 2, 8) };

        Assert.False(first.SharesDataIdsWith(second));
    }

    [Fact]
    public void Global_Data_Ids_Reach_Across_Row_Groups()
    {
        var first = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = DataIdSpace.Global(4, 2) };

        var second = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = DataIdSpace.Global(4, 2) };

        var local = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(4, 2, 7) };

        Assert.True(first.SharesDataIdsWith(second));

        Assert.False(first.SharesDataIdsWith(local));
    }

    /// <summary>
    /// A computed string column carries no data ids, so nothing can be compared against it without reading values
    /// </summary>
    [Fact]
    public void A_Column_Without_A_Space_Shares_Data_Ids_With_Nothing()
    {
        var scan = new BatchColumn { DataType = SqlDbType.VarChar, IdSpace = new DataIdSpace(4, 2, 7) };

        var computed = new BatchColumn { DataType = SqlDbType.VarChar };

        Assert.False(computed.SharesDataIdsWith(scan));

        Assert.False(scan.SharesDataIdsWith(computed));

        Assert.False(computed.SharesDataIdsWith(computed));
    }

    [Fact]
    public void A_Value_That_Does_Not_Normalize_Leaves_The_Null_Slot_Behind()
    {
        Assert.False(BatchSlotNormalizer.TryNormalize(long.MaxValue, out var integer));

        Assert.True(integer.IsNull);

        Assert.False(BatchSlotNormalizer.TryNormalize(1e100d, out var real));

        Assert.True(real.IsNull);

        Assert.False(BatchSlotNormalizer.TryNormalize(SqlDecimal.Parse("99999999999999999999"), out var numeric));

        Assert.True(numeric.IsNull);
    }

    [Fact]
    public void A_Null_Decimal_Normalizes_To_The_Null_Slot()
    {
        Assert.True(BatchSlotNormalizer.TryNormalize(SqlDecimal.Null, out var slot));

        Assert.True(slot.IsNull);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4294967296)]
    public void A_Data_Id_Outside_Its_Field_Is_Rejected(long dataId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BatchSlotNormalizer.FromDictionaryDataId(dataId));
    }

    private static BatchColumn Column(SqlDbType dataType, byte precision = 0, byte scale = 0)
        => new() { DataType = dataType, Precision = precision, Scale = scale };
}
