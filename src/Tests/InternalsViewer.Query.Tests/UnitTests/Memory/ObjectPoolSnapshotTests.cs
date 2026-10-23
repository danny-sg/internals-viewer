using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.Tests.UnitTests.Memory;

[Trait("Category", "Unit")]
public class ObjectPoolSnapshotTests
{
    private static readonly ObjectPoolEntry Dictionary
        = new(0x9000, 0x1_0040, 0x8000, 16, ColumnStoreObjectType.PrimaryDictionary, 6, 72057594048020480, 6, 0, 18796);

    private static readonly ObjectPoolEntry Segment
        = new(0x9100, 0x2_0000, 0x8100, 8, ColumnStoreObjectType.ColumnSegment, 6, 72057594048020480, 9, 3, 18796);

    [Fact]
    public void An_Entry_Is_Labelled_By_Its_Object()
    {
        Assert.Equal("Primary Dictionary (Column 6)", Dictionary.ToString());
        Assert.Equal("Segment (Rowgroup 3, Column 9)", Segment.ToString());
    }

    [Fact]
    public void A_Snapshot_Keeps_Its_Entries_Through_Export()
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            new ObjectPoolSnapshot([Dictionary, Segment]).Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.Equal([Dictionary, Segment], ObjectPoolSnapshot.Read(reader).Entries);
    }
}
