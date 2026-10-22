using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Tests;

public class BitPackedArrayTests
{
    [Fact]
    public void Values_Round_Trip_Across_Word_Boundaries()
    {
        int[] values = [.. Enumerable.Range(0, 200).Select(i => i * 37 % 101)];

        var packed = BitPackedArray.Pack(values);

        Assert.Equal(7, packed.Width);
        Assert.Equal(values, Enumerable.Range(0, packed.Count).Select(i => packed[i]));
    }

    [Fact]
    public void A_Single_Value_Takes_No_Bits()
    {
        var packed = BitPackedArray.Pack([0, 0, 0]);

        Assert.Equal(0, packed.Width);
        Assert.Equal(0, packed.Bytes);
        Assert.Equal([0, 0, 0], Enumerable.Range(0, packed.Count).Select(i => packed[i]));
    }

    [Fact]
    public void Sorted_Values_Are_Searched_Like_An_Array()
    {
        var packed = BitPackedArray.Pack([1, 3, 3, 3, 8, 20]);

        Assert.Equal(1, packed.GetFirstAtOrAfter(3));
        Assert.Equal(4, packed.GetFirstAfter(3));
        Assert.Equal(6, packed.GetFirstAtOrAfter(21));
        Assert.Equal(0, packed.GetFirstAtOrAfter(0));
    }

    [Fact]
    public void A_Packed_Array_Round_Trips_Through_Export()
    {
        var packed = BitPackedArray.Pack([5, 1_000_000, 42]);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            packed.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var read = BitPackedArray.Read(reader);

        Assert.Equal([5, 1_000_000, 42], Enumerable.Range(0, read.Count).Select(i => read[i]));
    }
}
