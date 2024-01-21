using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.Models;

public class DatabaseFile(DatabaseViewModel parent)
{
    public short FileId { get; set; }

    public int Size { get; set; }

    public DatabaseViewModel Parent { get; set; } = parent;
}