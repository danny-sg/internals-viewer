namespace InternalsViewer.Internals.Providers;

public class CurrentConnection(string connectionString, string databaseName)
{
    public string ConnectionString { get; } = connectionString;

    public string DatabaseName { get; } = databaseName;
}
