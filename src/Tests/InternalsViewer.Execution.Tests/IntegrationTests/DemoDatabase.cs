using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests;

/// <summary>
/// The demo database the integration tests read, and the structures within it they rely on
/// </summary>
/// <remarks>
/// Built by scripts/InternalsViewerDemo - Create script.sql. ClusteredTable holds 100,000 rows keyed 1 to 100,000 on Id, so a range of
/// keys can be predicted without reading the data first.
/// </remarks>
internal static class DemoDatabase
{
    public const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    public const string ClusteredTable = "ClusteredTable";

    public const string ClusteredIndex = "pk_ClusteredTable";

    public const string TextFieldIndex = "ix_ClusteredTable_TextField";

    public const string HeapTable = "HeapTable";

    public const string HeapIndex = "ix_HeapTable_Id";

    public const string CompressedTable = "CompressedTable";

    public const string CompressedIndex = "pk_CompressedTable";

    public const int ClusteredTableRowCount = 100_000;

    public static async Task<DatabaseSource> LoadAsync(TestServiceHost serviceHost)
    {
        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        return await serviceHost.GetService<IDatabaseService>()
                                .LoadAsync("TestDatabase", connection, CancellationToken.None);
    }

    /// <summary>
    /// Finds an allocation unit by table and index name, using an empty index name for a heap
    /// </summary>
    public static AllocationUnit Unit(DatabaseSource database, string table, string index = "")
        => database.AllocationUnits
                   .Values
                   .Single(a => a.TableName == table
                                && a.IndexName == index
                                && a.AllocationUnitType == AllocationUnitType.InRowData);
}
