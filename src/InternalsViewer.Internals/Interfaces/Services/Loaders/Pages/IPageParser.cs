using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

public interface IPageParser
{
    PageType[] SupportedPageTypes { get; }

    Page Parse(PageData page);
}

public interface IPageParser<out T> : IPageParser where T : Page, new()
{
    new T Parse(PageData page);
}