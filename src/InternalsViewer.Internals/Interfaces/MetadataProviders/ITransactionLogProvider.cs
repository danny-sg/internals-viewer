using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface ITransactionLogProvider
{
    Task Checkpoint();

    Task<List<TransactionLogEntry>> GetTransactionLog();
}