using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Allocation.Enums;

namespace InternalsViewer.Tests.Internals.UnitTests.Pages;

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
        var pfsPage = new PfsByte
        {
            PageSpaceFree = pageSpaceFree,
            GhostRecords = ghostRecords,
            Iam = iam,
            Mixed = mixed,
            Allocated = allocation
        };

        var result = pfsPage.ToString();

        Assert.Equal(expected, result);
    }
}
