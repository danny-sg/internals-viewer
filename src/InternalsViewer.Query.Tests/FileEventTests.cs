using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests;

public class FileEventTests
{
    [Fact]
    public void Computes_The_Page_Range_From_Offsets_Beyond_2Gb()
    {
        // The byte offset is 64-bit; dividing after a 32-bit cast wraps negative past 2 GB, corrupting the page range
        // for reads in the second half of a large data file.
        var read = new FileEvent
        {
            FileId = 1,
            Offset = 3_000_000_000,
            Size = 4 * 8_192,
        };

        Assert.Equal(366_210, read.FromPageAddress.PageId);
        Assert.Equal(366_214, read.ToPageAddress.PageId);
    }
}
