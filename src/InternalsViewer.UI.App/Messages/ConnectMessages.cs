using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.UI.App.Models.Connections;

namespace InternalsViewer.UI.App.Messages;

public class ConnectServerMessage(string connectionString, ServerConnectionSettings settings) : AsyncRequestMessage<bool>
{
    public string ConnectionString { get; } = connectionString;

    public ServerConnectionSettings Settings { get; set; } = settings;
}

public class ConnectFileMessage(string filename) : AsyncRequestMessage<bool>
{
    public string Filename { get; } = filename;
}

public class ConnectBackupMessage(string filename) : AsyncRequestMessage<bool>
{
    public string Filename { get; } = filename;
}