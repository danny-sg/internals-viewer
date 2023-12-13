namespace InternalsViewer.Internals.Engine.Database;

public class DatabaseInfo
{
    public int DatabaseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DatabaseState State { get; set; }

    public byte CompatibilityLevel { get; set; }
}