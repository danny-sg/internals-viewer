using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Tests.Internals.UnitTests.Helpers;

public class IdHelperTests
{
    [Theory]
    [InlineData(97, 256, 72057594044284928)]
    [InlineData(7, 2, 562949953880064)]
    public void Can_Get_AllocationUnitId_From_ObjectId_IndexId(int objectId, int indexId, long expected)
    {
        var result = IdHelpers.GetAllocationUnitId(objectId, indexId);

        Assert.Equal(expected, result);
    }
}
