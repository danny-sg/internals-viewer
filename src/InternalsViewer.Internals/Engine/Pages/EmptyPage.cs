using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Engine.Pages;

public sealed class EmptyPage : Page
{
    public EmptyPage()
    {
        PageHeader.PageType = PageType.None;
    }
}