using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.UI.App.Messages;

public sealed class OpenPageRequest(DatabaseSource database, PageAddress pageAddress)
{
    public PageAddress PageAddress { get; } = pageAddress;

    public DatabaseSource Database { get; } = database;

    public ushort? Slot { get; set; }

    public List<PageLogRecord> LogRecords { get; set; } = [];
}

public sealed class OpenIndexRequest(DatabaseSource database, PageAddress rootPage)
{
    public DatabaseSource Database { get; } = database;
    
    public PageAddress RootPageAddress { get; } = rootPage;
}

public sealed class OpenPageMessage(OpenPageRequest request) : AsyncRequestMessage<bool>
{
    public OpenPageRequest Request { get; } = request;
}

public sealed class OpenIndexMessage(OpenIndexRequest request) : AsyncRequestMessage<bool>
{
    public OpenIndexRequest Request { get; } = request;
}

public sealed class OpenColumnstoreRequest(DatabaseSource database, long allocationUnitId)
{
    public DatabaseSource Database { get; } = database;

    public long AllocationUnitId { get; } = allocationUnitId;
}

public sealed class OpenColumnstoreMessage(OpenColumnstoreRequest request) : AsyncRequestMessage<bool>
{
    public OpenColumnstoreRequest Request { get; } = request;
}