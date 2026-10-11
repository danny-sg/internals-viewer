namespace InternalsViewer.Query.Events.Transactions;

/// <summary>
/// sql_transaction transaction_state
/// </summary>
public enum TransactionState
{
    Begin = 0,
    Commit = 1,
    Rollback = 2,
}