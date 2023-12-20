using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services;
using InternalsViewer.Internals.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Records;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Internals;

public static class ServiceRegistration
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<CurrentConnection>();
        services.AddTransient<IPageReader, DatabasePageReader>();

        services.AddTransient<IServerInfoProvider, ServerInfoProvider>();

        services.AddTransient<IStructureInfoProvider, StructureInfoProvider>();
        //        services.AddTransient<ITransactionLogProvider, TransactionLogProvider>();
        services.AddTransient<IBufferPoolInfoProvider, BufferPoolInfoProvider>();

        services.AddTransient<IMetadataLoader, MetadataLoader>();

        services.AddTransient<IDatabaseLoader, DatabaseLoader>();

        services.AddTransient<IPageLoader, PageLoader>();
        services.AddTransient<IAllocationPageLoader, AllocationPageLoader>();
        services.AddTransient<IPfsPageLoader, PfsPageLoader>();
        services.AddTransient<IBootPageLoader, BootPageLoader>();

        services.AddTransient<IAllocationChainService, AllocationChainService>();
        services.AddTransient<IPfsChainService, PfsChainService>();
        services.AddTransient<IIamChainService, IamChainService>();

        services.AddTransient<ICompressionInfoService, CompressionInfoService>();
        services.AddTransient<IDictionaryService, DictionaryService>();
        services.AddTransient<ICompressedDataRecordService, CompressedDataRecordService>();

        services.AddTransient<IRecordService, RecordService>();

        services.AddTransient<ITableReader, TableReader>();
    }
}
