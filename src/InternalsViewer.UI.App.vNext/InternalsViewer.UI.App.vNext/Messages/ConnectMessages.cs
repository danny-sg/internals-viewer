using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.UI.App.vNext.Messages;

public class ConnectServerMessage(string connectionString) : ValueChangedMessage<string>(connectionString);

public class ConnectFileMessage(string filename) : ValueChangedMessage<string>(filename);

public class ConnectBackupMessage(string filename) : ValueChangedMessage<string>(filename);

