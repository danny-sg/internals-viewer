using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.UI.App.ViewModels.Index;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.UI.App.Tests.ViewModels.Index;

[Trait("Category", "Unit")]
public class PageRowResolverTests
{
    private static readonly PageAddress Address = new(1, 405);

    private static readonly PageAddress OtherAddress = new(1, 406);

    [Fact]
    public void An_Offset_Inside_A_Record_Selects_Its_Slot()
    {
        var resolver = Resolver();

        resolver.MoveTo(Address, 0x95, CancellationToken.None);

        Assert.Equal(new RowIdentifier(Address, 1), resolver.Row);
    }

    [Fact]
    public void An_Offset_Outside_Every_Record_Keeps_The_Row_On_The_Same_Page()
    {
        var resolver = Resolver();

        resolver.MoveTo(Address, 0x95, CancellationToken.None);

        resolver.MoveTo(Address, 0x62, CancellationToken.None);

        Assert.Equal(new RowIdentifier(Address, 1), resolver.Row);
    }

    [Fact]
    public void An_Offset_Outside_Every_Record_On_Another_Page_Selects_No_Row()
    {
        var resolver = Resolver();

        resolver.MoveTo(Address, 0x95, CancellationToken.None);

        resolver.MoveTo(OtherAddress, 0x62, CancellationToken.None);

        Assert.Null(resolver.Row);
    }

    private static PageRowResolver Resolver() => new(new PageSource(), new DatabaseSource(null!), NullLogger.Instance);

    private sealed class PageSource : IPageService
    {
        public Task<Page> GetPage(DatabaseSource database,
                                  PageAddress pageAddress,
                                  CancellationToken cancellationToken,
                                  bool isMarkEnabled = true)
            => Task.FromResult(Create(pageAddress));

        public Task<Page> GetPage(DatabaseSource database, PageAddress pageAddress, byte[] buffer, CancellationToken cancellationToken)
            => Task.FromResult(Create(pageAddress));

        public Task<T> GetPage<T>(DatabaseSource database,
                                  PageAddress pageAddress,
                                  CancellationToken cancellationToken,
                                  bool isMarkEnabled = true)
            where T : Page
            => Task.FromResult((T)Create(pageAddress));

        public Page ParsePage(DatabaseSource database, PageAddress pageAddress, byte[] data) => Create(pageAddress);

        public void ResetCache(DatabaseSource database)
        {
        }

        private static Page Create(PageAddress pageAddress) => new EmptyPage { PageAddress = pageAddress, OffsetTable = [0x70, 0x90] };
    }
}
