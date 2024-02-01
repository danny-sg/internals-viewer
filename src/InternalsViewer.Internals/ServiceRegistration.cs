using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Server;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Loaders.Chains;
using InternalsViewer.Internals.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Services.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Internals.Services.Records;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace InternalsViewer.Internals;

[ExcludeFromCodeCoverage]
public static class ServiceRegistration
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddTransient<IPageReader, QueryPageReader>();

        services.AddTransient<IServerInfoProvider, ServerInfoProvider>();

        //        services.AddTransient<ITransactionLogProvider, TransactionLogProvider>();
        services.AddTransient<IBufferPoolInfoProvider, BufferPoolInfoProvider>();

        services.AddTransient<IMetadataLoader, MetadataLoader>();

        services.AddTransient<IDatabaseLoader, DatabaseLoader>();

        services.AddTransient<IRecordReader, RecordReader>();

        services.AddTransient<IPageLoader, PageLoader>();

        services.AddTransient<IAllocationChainService, AllocationChainService>();
        services.AddTransient<IPfsChainService, PfsChainService>();
        services.AddTransient<IIamChainService, IamChainService>();

        services.AddTransient<IRecordService, RecordService>();

        services.AddTransient<IPageService, PageService>();

        services.AddTransient<CompressionInfoLoader>();

        services.AddTransient<FixedVarRecord>();
        services.AddTransient<CdDataRecordLoader>();
        services.AddTransient<IndexFixedVarRecordLoader>();

        RegisterPageParsers(services);
    }

    private static void RegisterPageParsers(IServiceCollection services)
    {
        services.AddTransient<IPageParser, EmptyPageParser>();
        services.AddTransient<IPageParser, AllocationPageParser>();
        services.AddTransient<IPageParser, BootPageParser>();
        services.AddTransient<IPageParser, DataPageParser>();
        services.AddTransient<IPageParser, IamPageParser>();
        services.AddTransient<IPageParser, IndexPageParser>();
        services.AddTransient<IPageParser, LobPageParser>();
        services.AddTransient<IPageParser, PfsPageParser>();
        services.AddTransient<IPageParser, FileHeaderPageParser>();
    }
}
