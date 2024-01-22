﻿using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.UI.App.Messages;

public class OpenPageRequest(DatabaseSource database, PageAddress pageAddress)
{
    public PageAddress PageAddress { get; set; } = pageAddress;

    public DatabaseSource Database { get; set; } = database;

    public ushort? Slot { get; set; }
}

public class OpenPageMessage(OpenPageRequest request) : AsyncRequestMessage<bool>
{
    public OpenPageRequest Request { get; } = request;
}