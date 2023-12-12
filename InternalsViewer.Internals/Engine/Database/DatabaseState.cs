namespace InternalsViewer.Internals.Engine.Database;

public enum DatabaseState: byte
{
    Online = 0,
    Restoring = 1,
    Recovering = 2,
    RecoveryPending = 3,
    Suspect = 4,
    Emergency = 5,
    Offline = 6,
    Copying = 7,
    OfflineSecondary = 10
}