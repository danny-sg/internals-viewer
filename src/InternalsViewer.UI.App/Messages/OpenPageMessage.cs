using System;
using ABI.System;
using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using static CommunityToolkit.WinUI.Animations.Expressions.ExpressionValues;

namespace InternalsViewer.UI.App.Messages;

public class OpenPageRequest(DatabaseSource database, PageAddress pageAddress)
{
    public PageAddress PageAddress { get; } = pageAddress;

    public DatabaseSource Database { get; } = database;

    public ushort? Slot { get; set; }
}

public class OpenPageMessage(OpenPageRequest request) : AsyncRequestMessage<bool>
{
    public OpenPageRequest Request { get; } = request;
}