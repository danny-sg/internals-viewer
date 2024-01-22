using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.UI.App.Models.Connections;

namespace InternalsViewer.UI.App.Messages;

public class ConnectServerMessage(string connectionString, RecentConnection recent) : AsyncRequestMessage<bool>
{
    public string ConnectionString { get; } = connectionString;

    public RecentConnection Recent { get; } = recent;
}

public class ConnectFileMessage(string filename, RecentConnection recent) : AsyncRequestMessage<bool>
{
    public string Filename { get; } = filename;

    public RecentConnection Recent { get; } = recent;
}

public class ConnectBackupMessage(string filename, RecentConnection recent) : AsyncRequestMessage<bool>
{
    public string Filename { get; } = filename;

    public RecentConnection Recent { get; set; } = recent;
}

public class ConnectRecentMessage(RecentConnection recent) : AsyncRequestMessage<bool>
{
    public RecentConnection Recent { get; set; } = recent;
}