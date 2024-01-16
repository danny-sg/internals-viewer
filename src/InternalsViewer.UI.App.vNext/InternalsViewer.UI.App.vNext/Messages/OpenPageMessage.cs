using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.UI.App.vNext.Messages;

public class OpenPageRequest(DatabaseSource database, PageAddress pageAddress)
{
    public PageAddress PageAddress { get; set; } = pageAddress;

    public DatabaseSource Database { get; set; } = database;

    public string Target { get; set; } = string.Empty;
}

public class OpenPageMessage(OpenPageRequest request) : ValueChangedMessage<OpenPageRequest>(request);