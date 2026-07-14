namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// Broad intent a <see cref="LockMode"/> falls under, collapsing the ~20 modes into the handful that matter at a glance
/// </summary>
/// <remarks>
/// Groups the shared/intent-shared family as <see cref="Read"/>, the update family as <see cref="Update"/>, the
/// exclusive/intent-exclusive family as <see cref="Write"/>, schema modification as <see cref="Schema"/>, the
/// serializable key-range modes as <see cref="Range"/> and bulk update as <see cref="Bulk"/>.
/// </remarks>
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
    /// Whether a mode is a full lock that SUPERSEDES finer locks (the target of a lock escalation), as opposed to a
    /// pure intent mode (IS/IU/IX) that is held alongside them
    /// </summary>
    public static bool IsSuperseding(LockMode mode) =>
        mode is LockMode.X or LockMode.U or LockMode.S or LockMode.SIX or LockMode.UIX;
}
