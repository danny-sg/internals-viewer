namespace InternalsViewer.Internals.Engine.Database.Enums;

/// <summary>
/// Database States
/// </summary>
/// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/databases/database-states"/>
public enum DatabaseState : byte
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