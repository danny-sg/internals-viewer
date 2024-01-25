namespace InternalsViewer.UI.App.Models;

public class ExtentAllocation(short fileId, int extentId)
{
    public int ExtentId { get; } = extentId;

    public short FileId { get; } = fileId;
}