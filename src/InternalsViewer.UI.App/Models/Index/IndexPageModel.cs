using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Models.Index;

public class IndexPageModel
{
    public  PageAddress PageAddress { get; set; }

    public PageAddress NextPage { get; set; }

    public PageAddress PreviousPage { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Slots { get; set; }

    public int Level { get; set; }
}