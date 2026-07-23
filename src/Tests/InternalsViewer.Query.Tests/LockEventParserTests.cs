using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Tests;

public class LockEventParserTests
{
    // A KEY lock's %%lockres%% hash is the high word of resource_1 byte-swapped, then resource_2 byte-swapped, as 12
    // hex digits. Cases verified against real dbo.LockEscalationDemo locks (resource_0 / low word of resource_1 are a
    // constant header, not part of the hash).
    [Theory]
    [InlineData(0x94810100UL, 0xa0843244UL, "(8194443284a0)")]
    [InlineData(0xa0610100UL, 0x1c40bd6aUL, "(61a06abd401c)")]
    [InlineData(0xec980100UL, 0x10a52a01UL, "(98ec012aa510)")]
    [InlineData(0xc9a00100UL, 0x65c9a336UL, "(a0c936a3c965)")]
    [InlineData(0x07010000UL, 0x817b5918UL, "(010718597b81)")] // leading zero is padded to 12 digits
    public void BuildKeyHash_Produces_The_LockRes_Format(ulong resource1, ulong resource2, string expected)
    {
        Assert.Equal(expected, LockEventParser.BuildKeyHash(resource1, resource2));
    }
}
