using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Engine.Pages;

public class EmptyPage : Page
{
    public EmptyPage()
    {
        PageHeader.PageType = PageType.None;
    }
}