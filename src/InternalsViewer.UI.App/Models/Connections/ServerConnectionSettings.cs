namespace InternalsViewer.UI.App.Models.Connections;

public class ServerConnectionSettings
{
    public string InstanceName { get; init; } = string.Empty;

    public int AuthenticationType { get; init; }

    public string DatabaseName { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;
}
