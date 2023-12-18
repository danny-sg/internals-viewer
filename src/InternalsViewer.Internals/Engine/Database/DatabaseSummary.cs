using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.Internals.Engine.Database;

public class DatabaseSummary
{
    public int DatabaseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DatabaseState State { get; set; }

    public byte CompatibilityLevel { get; set; }
}