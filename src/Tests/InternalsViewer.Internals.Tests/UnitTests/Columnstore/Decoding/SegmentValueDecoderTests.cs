using System.Globalization;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Internals.Tests.UnitTests.Columnstore.Decoding;

[Trait("Category", "Unit")]
[Trait("Area", "Columnstore")]
public class SegmentValueDecoderTests
{
    [Theory]
    [InlineData(1000, 0.01, 999, "19.99")]
    [InlineData(0, 0.1, 3, "0.3")]
    [InlineData(-5, 0.0001, 0, "-0.0005")]
    [InlineData(10, 100, 2, "1200")]
    public void A_Value_Based_Data_Id_Is_Scaled_By_The_Magnitude_Exactly(long baseId, double magnitude, long dataId, string expected)
    {
        var decoder = new SegmentValueDecoder(new ColumnSegment { BaseId = baseId, Magnitude = magnitude }, null);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), decoder.Decode(dataId));
    }
}
