namespace InternalsViewer.Query.Events.Locks;

public sealed record LockOwnerContext
{
    public LockOwnerType OwnerType { get; set; }

    public long? TransactionId { get; init; }
    
    public int? SessionId { get; init; }

    public ulong WorkspaceId { get; set; }

    public uint SubId { get; set; }

    public uint NestId { get; set; }
}