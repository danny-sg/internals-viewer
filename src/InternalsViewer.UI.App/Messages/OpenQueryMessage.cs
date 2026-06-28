using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.UI.App.Messages;

public sealed class OpenQueryMessage(DatabaseSource database) : AsyncRequestMessage<bool>
{
    public DatabaseSource Database { get; } = database;
}