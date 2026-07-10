namespace InternalsViewer.Query;

internal static class EventConstants
{
    public static readonly string[] Events =
    [
        "sqlserver.sql_batch_starting",
        "sqlserver.sql_batch_completed",
        "sqlserver.file_write_completed",
        "sqlserver.log_flush_complete",
        "sqlserver.page_split",
        "sqlserver.query_thread_profile",
        "sqlserver.file_read",
        "sqlserver.file_read_completed",
        "sqlserver.physical_page_read",
        "sqlserver.physical_page_write",
        "sqlserver.query_post_execution_showplan"
    ];

    public static readonly string[] LockEvents =
    [
        "sqlserver.lock_acquired",
        "sqlserver.lock_released",
    ];

    public static readonly string[] WaitEvents =
    [
        "sqlos.wait_info"
    ];

    public static readonly string[] LogEvents =
    [
        "sqlserver.transaction_log"
    ];

    public static readonly string[] MemoryEvents =
    [
        "sqlserver.query_memory_grant_usage",
        "sqlserver.hash_spill_details",
        "sqlserver.sort_warning",
        "sqlserver.memory_grant_updated_by_feedback"
    ];

    public static readonly string[] LatchEvents =
    [
        "sqlserver.latch_suspend_begin",
        "sqlserver.latch_suspend_end",
        "sqlserver.latch_acquired",
        "sqlserver.latch_released",
        "sqlserver.latch_demoted",
        "sqlserver.latch_promoted"
    ];

    public static readonly string[] Actions =
    [
        "sqlserver.session_id",
        "sqlserver.request_id",
        "sqlserver.sql_text",
        "sqlserver.database_id",
        "sqlserver.plan_handle",
        "sqlserver.transaction_id",
        "package0.event_sequence",
        "sqlos.task_address",
        "sqlos.worker_address"
    ];

    public static readonly string[] CallstackActions =
    [
        "package0.callstack"
    ];
}
