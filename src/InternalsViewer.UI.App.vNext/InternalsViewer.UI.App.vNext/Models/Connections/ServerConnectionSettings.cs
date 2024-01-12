namespace InternalsViewer.UI.App.vNext.Models.Connections;

public class ServerConnectionSettings
{
    public string InstanceName { get; set; } = string.Empty;

    public int? AuthenticationType { get; set; }

    public string DatabaseName { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
}
