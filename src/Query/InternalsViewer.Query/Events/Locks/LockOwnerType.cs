namespace InternalsViewer.Query.Events.Locks;

// ReSharper disable IdentifierTypo
public enum LockOwnerType
{
    Unknown = 0,
    Transaction = 1,
    Cursor = 2,
    Session = 3,
    SharedXactWorkspace = 4,
    ExclusiveXactWorkspace = 5,
    LockConflictNotificationObject = 6,
    LockTableIterator = 7,
    Node = 8,
    LastLockInfoOwner = 9
}