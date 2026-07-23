using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query;

public sealed record EventOptions
{
    /// <summary>
    /// The lock mode categories to surface — locks are captured for every mode (event grouping needs them) then narrowed
    /// to these before the query is cropped, so a deselected mode can't widen the crop window
    /// </summary>
    public HashSet<LockModeCategory> IncludeLockModeCategories { get; set; } = DefaultLockModeCategories();

    /// <summary>
    /// Whether any locks are shown, derived from <see cref="IncludeLockModeCategories"/>
    /// </summary>
    public bool IncludeLock => IncludeLockModeCategories.Count > 0;

    public bool IncludeWait { get; set; } = true;

    public bool IncludeMemory { get; set; }

    public bool IncludeCallStack { get; set; }

    public bool IncludeLatch { get; set; } = true;

    public bool IncludeSystemObjects { get; set; } 

    /// <summary>
    /// Trim events (and the call stack) outside the executed query's time window, dropping surrounding noise
    /// </summary>
    public bool CropToQuery { get; set; } = true;

    /// <summary>
    /// Maximum size (MB) of the XEvent (.xel) trace; the file rolls over at this size and (with a single rollover file)
    /// the earlier events are discarded, so it acts as a hard cap. When 0 the SQL Server default (1 GB) applies
    /// </summary>
    public int MaxTraceSizeMb { get; set; } = 150;

    /// <summary>
    /// Directory the XEvent (.xel) file target is written to; when null/empty the SQL Server log directory is used
    /// </summary>
    /// <remarks>
    /// A custom directory must be writable by the SQL Server service account (it, not the client, writes the file).
    /// Local SQL Server only — a path here is on the server's file system.
    /// </remarks>
    public string? TraceDirectory { get; set; }

    /// <summary>
    /// Delete the .xel trace file(s) once read; only applies to a custom <see cref="TraceDirectory"/> (the SQL Server
    /// log directory is not writable by the client)
    /// </summary>
    public bool AutoDeleteTrace { get; set; } = true;

    /// <summary>
    /// The default lock categories: every category except Schema (noisy) and None (not a real category)
    /// </summary>
    public static HashSet<LockModeCategory> DefaultLockModeCategories() =>
        [.. Enum.GetValues<LockModeCategory>().Where(c => c is not LockModeCategory.None and not LockModeCategory.Schema)];
}