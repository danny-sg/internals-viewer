namespace InternalsViewer.Query.Events.Locks;

// ReSharper disable InconsistentNaming
public enum LockResult
{
    OK = 0,
    OKWAIT = 1,
    TIMEDOUT = -1,
    CANCEL = -2,
    DEADLOCK = -3,
    TMABORT = -4,
    RESULTS_IN_DISTRIBUTED_MODE = -5,
    KILL_EXISTS = -6,
    KILL_BLOCKED_BY_SYSTEM_TASK = -7,
    FIDO_PARSING_FAILED = -8,
    FIDO_MANAGER_NOT_FOUND = -9,
    FIDO_MANAGER_CANCEL = -10
}
