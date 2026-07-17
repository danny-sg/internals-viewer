namespace InternalsViewer.Query.Events.Transactions;

/// <summary>
/// Transaction lifecycle point (begin/commit/rollback)
/// </summary>
/// <remarks>
/// Captured for lock timing. A lock held to the end of its transaction has no <c>lock_released</c> inside the traced window, so the
/// commit/rollback here is its measured release point (matched on <see cref="TransactionId"/> — see <see cref="Consolidation.HeldLockCloser"/>).
/// </remarks>
public sealed record TransactionEvent : EngineEvent
{
    public long TransactionId { get; init; }

    public TransactionState State { get; init; }

    /// <summary>
    /// Whether this point ends the transaction, releasing everything it holds
    /// </summary>
    public bool IsEnd => State is TransactionState.Commit or TransactionState.Rollback;

    public override string Description => $"Transaction {State}";

    public override bool IsVisible => false;
}