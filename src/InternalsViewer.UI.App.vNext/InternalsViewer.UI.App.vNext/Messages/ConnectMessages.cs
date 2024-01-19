using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.UI.App.vNext.Models.Connections;

namespace InternalsViewer.UI.App.vNext.Messages;

public class ConnectServerMessage(string connectionString, ServerConnectionSettings settings) : AsyncRequestMessage<bool>
{
    public string ConnectionString { get; set; } = connectionString;

    public ServerConnectionSettings Settings { get; set; } = settings;
}

public class ConnectFileMessage(string filename) : ValueChangedMessage<string>(filename);

public class ConnectBackupMessage(string filename) : ValueChangedMessage<string>(filename);

