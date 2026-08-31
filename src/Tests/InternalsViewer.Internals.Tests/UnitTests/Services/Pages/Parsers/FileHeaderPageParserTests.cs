using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Pages.Parsers;

[Trait("Category", "Unit")]
[Trait("Area", "Pages")]
public class FileHeaderPageParserTests
{
    private const int RecordOffset = 0x529;

    /// <summary>
    /// File header record from the InternalsViewerDemo database, page (1:0) slot 0
    /// </summary>
    private const string RecordHex =
          "300008000000000042000000000000000000004100A700A700A900AB00AF00B3"
        + "00B700BB00C500CF00D900D900DD00E100E500E900F3000F01190123012D013D"
        + "01470157015B01650165018B019B019B019B019B019B019B019B01AB01AB01AB"
        + "01B501BF01DB01E501F5011102190271047104710473047D0481048504890491"
        + "049B049F04A304A704AB04B504B904BD04C104C504C9040781E53B46FF1149A5"
        + "33B14102B607660100010000240000FFFFFFFF00200000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000040000000000"
        + "00FFFFFFFF000200000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "000000000000002C5116AFB0250E4984D52D6805D8D61A000000000000000000"
        + "000000000049006E007400650072006E0061006C007300560069006500770065"
        + "007200440065006D006F00000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "0000000000000000000000000000000000010000000000000000000000000000"
        + "0000000000000000000000000000000000000000000000000000000000000000"
        + "000000000000000000000000000000000000000000FFFFFFFFFFFFFFFFFFFFFF"
        + "FFFFFFFFFF00000000";

    private static PageData BuildPage()
    {
        var data = new byte[PageData.Size];

        Convert.FromHexString(RecordHex).CopyTo(data, RecordOffset);

        BitConverter.GetBytes((ushort)RecordOffset).CopyTo(data, PageData.Size - 2);

        return new PageData
        {
            Data = data,
            PageAddress = new PageAddress(1, 0),
            PageHeader = new PageHeader { SlotCount = 1 },
            IsMarkEnabled = true
        };
    }

    [Fact]
    public void Can_Parse_File_Header_Page()
    {
        var page = new FileHeaderPageParser().Parse(BuildPage());

        Assert.Equal("InternalsViewerDemo", page.LogicalName);

        Assert.Equal(Guid.Parse("3be58107-ff46-4911-a533-b14102b60766"), page.BindingId);

        Assert.Equal(Guid.Parse("af16512c-25b0-490e-84d5-2d6805d8d61a"), page.FileIdGuid);

        Assert.Equal(Guid.Empty, page.DifferentialBaseGuid);

        Assert.Equal(1, page.FileId);

        Assert.Equal(1, page.FileGroupId);

        Assert.Equal(9216, page.FileSize);

        Assert.Equal(-1, page.MaxSize);

        Assert.Equal(1024, page.MinSize);

        Assert.Equal(-1, page.UserShrinkSize);

        Assert.Equal(8192, page.Growth);

        Assert.Equal(512, page.SectorSize);

        Assert.Equal(0, page.Perf);

        Assert.Equal(0, page.Status);

        Assert.Equal(0, page.RestoreStatus);
    }

    [Fact]
    public void Unset_Lsns_Parse_As_Zero()
    {
        var page = new FileHeaderPageParser().Parse(BuildPage());

        Assert.Equal(default, page.BackupLsn);
        Assert.Equal(default, page.FirstUpdateLsn);
        Assert.Equal(default, page.OldestRestoredLsn);
        Assert.Equal(default, page.MaxLsn);
        Assert.Equal(default, page.FirstLsn);
        Assert.Equal(default, page.CreateLsn);
        Assert.Equal(default, page.DifferentialBaseLsn);
        Assert.Equal(default, page.FileOfflineLsn);
        Assert.Equal(default, page.RestoreRedoStartLsn);
    }

    [Fact]
    public void Markers_Are_Positioned_On_The_Column_Data()
    {
        var page = new FileHeaderPageParser().Parse(BuildPage());

        var bindingId = Assert.Single(page.MarkItems.OfType<PropertyItem>(), i => i.PropertyName == nameof(FileHeaderPage.BindingId));

        Assert.Equal(RecordOffset + 151, bindingId.Offset);
        Assert.Equal(16, bindingId.Length);

        var logicalName = Assert.Single(page.MarkItems.OfType<PropertyItem>(), i => i.PropertyName == nameof(FileHeaderPage.LogicalName));

        Assert.Equal(RecordOffset + 357, logicalName.Offset);
        Assert.Equal(38, logicalName.Length);
    }

    [Fact]
    public void Empty_Page_Does_Not_Throw()
    {
        var page = new FileHeaderPageParser().Parse(new PageData
        {
            Data = new byte[PageData.Size],
            PageHeader = new PageHeader { SlotCount = 0 }
        });

        Assert.Equal(string.Empty, page.LogicalName);
        Assert.Equal(Guid.Empty, page.BindingId);
    }
}
