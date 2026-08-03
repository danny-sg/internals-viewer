using System.Data;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths.Joins;

public class JoinHashTests
{
    [Fact]
    public void Same_Key_Hashes_The_Same()
    {
        Assert.True(JoinHash.TryCompute(Key(4417), 1, out var first));
        Assert.True(JoinHash.TryCompute(Key(4417), 1, out var second));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_Keys_Hash_Differently()
    {
        Assert.True(JoinHash.TryCompute(Key(4417), 1, out var first));
        Assert.True(JoinHash.TryCompute(Key(4418), 1, out var second));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Null_Key_Cannot_Be_Hashed()
    {
        var key = AccessKey.Create(AccessValue.FromNull(SqlDbType.Int).WithColumnName("Id"));

        Assert.False(JoinHash.TryCompute(key, 1, out var hash));
        Assert.Equal(0u, hash);
    }

    [Fact]
    public void Null_In_Any_Column_Cannot_Be_Hashed()
    {
        var key = AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, 1).WithColumnName("Id"),
                                   AccessValue.FromNull(SqlDbType.Int).WithColumnName("Other"));

        Assert.False(JoinHash.TryCompute(key, 2, out _));
    }

    [Fact]
    public void Column_Order_Changes_The_Hash()
    {
        var first = AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, 1),
                                     AccessValue.FromInteger(SqlDbType.Int, 2));

        var second = AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, 2),
                                      AccessValue.FromInteger(SqlDbType.Int, 1));

        Assert.True(JoinHash.TryCompute(first, 2, out var firstHash));
        Assert.True(JoinHash.TryCompute(second, 2, out var secondHash));

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void A_Null_Key_Still_Hashes_So_The_Row_Can_Be_Bucketed()
    {
        var key = AccessKey.Create(AccessValue.FromNull(SqlDbType.Int).WithColumnName("Id"));

        Assert.True(JoinHash.HasNull(key, 1));

        var first = JoinHash.Compute(key, 1);

        Assert.Equal(first, JoinHash.Compute(key, 1));
        Assert.InRange(JoinHash.GetBucket(first, 4), 0, 15);
    }

    [Fact]
    public void A_Null_Column_Does_Not_Hash_As_Zero()
    {
        var nullKey = AccessKey.Create(AccessValue.FromNull(SqlDbType.Int).WithColumnName("Id"));

        var zeroKey = AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, 0).WithColumnName("Id"));

        Assert.NotEqual(JoinHash.Compute(nullKey, 1), JoinHash.Compute(zeroKey, 1));
    }

    [Fact]
    public void Has_Null_Finds_A_Null_In_Any_Position()
    {
        var trailing = AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, 1),
                                        AccessValue.FromNull(SqlDbType.Int));

        var leading = AccessKey.Create(AccessValue.FromNull(SqlDbType.Int),
                                       AccessValue.FromInteger(SqlDbType.Int, 1));

        Assert.True(JoinHash.HasNull(trailing, 2));
        Assert.True(JoinHash.HasNull(leading, 2));

        Assert.False(JoinHash.HasNull(trailing, 1));
    }

    [Fact]
    public void Bucket_Is_Within_Range()
    {
        for (var value = 0; value < 1000; value++)
        {
            Assert.True(JoinHash.TryCompute(Key(value), 1, out var hash));

            var bucket = JoinHash.GetBucket(hash, 4);

            Assert.InRange(bucket, 0, 15);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(4096)]
    [InlineData(65536)]
    public void Strided_Keys_Spread_Across_Buckets(int stride)
    {
        var counts = new int[16];

        for (var index = 0; index < 1600; index++)
        {
            Assert.True(JoinHash.TryCompute(Key(index * stride), 1, out var hash));

            counts[JoinHash.GetBucket(hash, 4)]++;
        }

        Assert.All(counts, c => Assert.True(c > 0, "every bucket should receive rows"));

        Assert.True(counts.Max() < 200, $"worst bucket held {counts.Max()} of 1600 rows, expected near 100");
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(61, 6)]
    [InlineData(64, 6)]
    [InlineData(65, 7)]
    [InlineData(512, 9)]
    [InlineData(1000000, 9)]
    public void Bucket_Bits_Round_Row_Count_Up_To_A_Power_Of_Two(long rows, int expected)
    {
        Assert.Equal(expected, JoinHash.BucketBitsFor(rows));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Bucket_Bits_Fall_Back_When_The_Row_Count_Is_Unknown(long rows)
    {
        Assert.Equal(JoinHash.DefaultBucketBits, JoinHash.BucketBitsFor(rows));
    }

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
