using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.UI.App.vNext.Models.Connections;

namespace InternalsViewer.UI.App.vNext.Messages;

public class ConnectServerMessage((string ConnectionString, ServerConnectionSettings settings) value)
    : ValueChangedMessage<(string ConnectionString, ServerConnectionSettings settings)>(value);

public class ConnectFileMessage(string filename) : ValueChangedMessage<string>(filename);

public class ConnectBackupMessage(string filename) : ValueChangedMessage<string>(filename);

