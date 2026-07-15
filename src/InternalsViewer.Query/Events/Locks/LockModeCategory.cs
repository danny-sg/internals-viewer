namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// Categorisation of lock modes by grouped intent
/// </summary>
public enum LockModeCategory
{
    None,
    Read,
    Update,
    Write,
    Schema,
    Range,
    Bulk
}

public static class LockModeClassifier
{
    /// <summary>
    /// Categorises a lock mode by its broad intent
    /// </summary>
    public static LockModeCategory Categorise(LockMode mode) => mode switch
    {
        LockMode.SCH_S or LockMode.S or LockMode.IS => LockModeCategory.Read,

        LockMode.U or LockMode.IU or LockMode.SIU => LockModeCategory.Update,

        LockMode.X or LockMode.IX or LockMode.SIX or LockMode.UIX => LockModeCategory.Write,

        LockMode.SCH_M => LockModeCategory.Schema,

        LockMode.BU => LockModeCategory.Bulk,

        // Every RS_/RI_/RX_ range mode (13..20) protects a serializable key range.
        >= LockMode.RS_S and <= LockMode.RX_U => LockModeCategory.Range,

        _ => LockModeCategory.None
    };

    /// <summary>
    /// Whether a mode is a pure intent mode
    /// </summary>
    /// <remarks>
    /// An intent lock is a declaration rather than a hold, so it ranks below every real lock however exclusive its category (an IU is
    /// weaker than an RS_U, despite both being of the update family).
    /// </remarks>
    public static bool IsIntent(LockMode mode) => mode is LockMode.IS or LockMode.IU or LockMode.IX;

    /// <summary>
    /// Whether a mode is a full lock that supersedes finer locks
    /// </summary>
    /// <remarks>
    /// Lock Mode hierarchy is:
    /// 
    ///     X           Exclusive
    ///     ├─ UIX      Update with Intent Exclusive
    ///     │  └─ U     Update
    ///     └─ SIX      Shared with Intent Exclusive
    ///     ├─ SIU      Shared with Intent Update
    ///     └─ S        Shared
    ///     ├─ IX       Intent Exclusive
    ///     │  └─ IU    Intent Update
    ///     │     └─ IS Intent Shared
    /// </remarks>
    public static bool IsSuperseding(LockMode mode) =>
        mode is LockMode.X or LockMode.U or LockMode.S or LockMode.SIX or LockMode.UIX;
}
