namespace InternalsViewer.Internals.Tests.UnitTests.Pages;

public class PfsByteTests
{
    [Theory]
    [InlineData(SpaceFree.Empty, false, false, false, false, "PFS Status: Not Allocated | 0% Full")]
    [InlineData(SpaceFree.FiftyPercent, false, false, false, false, "PFS Status: Not Allocated | 50% Full")]
    [InlineData(SpaceFree.OneHundredPercent, false, false, false, false, "PFS Status: Not Allocated | 100% Full")]
    [InlineData(SpaceFree.OneHundredPercent, false, true, false, false, "PFS Status: Not Allocated | 100% Full | IAM Page")]
    [InlineData(SpaceFree.OneHundredPercent, false, true, true, true, "PFS Status: Allocated | 100% Full | IAM Page | Mixed Extent")]
    [InlineData(SpaceFree.EightyPercent, true, true, true, true, "PFS Status: Allocated | 80% Full | IAM Page | Mixed Extent | Has Ghost")]
    public void Can_Get_ToString_Description (SpaceFree pageSpaceFree,
                                              bool ghostRecords,
                                              bool iam,
                                              bool mixed,
                                              bool allocation,
                                              string expected)
    {
        var value = (byte)((byte)pageSpaceFree
                           | (ghostRecords ? 0x08 : 0)
                           | (iam ? 0x10 : 0)
                           | (mixed ? 0x20 : 0)
                           | (allocation ? 0x40 : 0));

        var pfsByte = new PfsByte(value);

        var result = pfsByte.ToString();

        Assert.Equal(expected, result);
    }
}
