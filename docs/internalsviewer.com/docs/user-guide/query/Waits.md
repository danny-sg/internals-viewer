# Waits

The Wait band shows waits captured during the query - points where the query had to stop and wait for a resource to become available, such as a lock, a latch, or an I/O to complete. Each wait shows its [wait type](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-os-wait-stats-transact-sql), the same names seen in `sys.dm_os_wait_stats`.

Waits are reported in whole milliseconds, so very short waits can show a duration of 0ms. For page I/O specifically, the corresponding [latch suspend](/docs/user-guide/query/Latches) gives a more precise picture of how long the wait actually took.

Waits are only captured when enabled - see the [Events menu](/docs/user-guide/query#events-menu).
