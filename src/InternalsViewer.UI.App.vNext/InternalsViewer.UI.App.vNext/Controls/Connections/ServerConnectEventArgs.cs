using System;
using InternalsViewer.UI.App.vNext.Models.Connections;

namespace InternalsViewer.UI.App.vNext.Controls.Connections;

public class ServerConnectEventArgs(string connectionString, ServerConnectionSettings? settings = null) : EventArgs
{
    public string ConnectionString { get; set; } = connectionString;

    public ServerConnectionSettings? Settings { get; } = settings;
}