# Permissions

A SQL Server connection needs the `sysadmin` role. Data files and backups need nothing beyond read access to the files.

Pages are read through the server with `DBCC PAGE`, which returns any page of the database regardless of who is allowed to see the data in it, so SQL Server restricts it to `sysadmin`. A login without the role can still connect, but the database fails to load on its first page read with "Error reading page 1:9".

`DBCC PAGE` is not the only thing Internals Viewer runs on the server. A SQL Server connection also uses:

- **`SERVERPROPERTY` and `IS_SRVROLEMEMBER('sysadmin')`** - when connecting, to record the server's version and whether the login has `sysadmin` in the [Diagnostic log](/docs/user-guide/settings#diagnostic-log)
- **`sys.databases`** - to fill the **Database** list on the SQL Server page
- **`sys.dm_os_buffer_descriptors`** - for the [Buffer Pool](/docs/user-guide/allocations#buffer-pool) overlay

The [Query](/docs/user-guide/query) view does more, since it traces the query it runs:

- **Extended Events** - a session is created, started, stopped and dropped around each run, writing to the **Trace path** in [Settings](/docs/user-guide/settings#trace-path)
- **`CHECKPOINT` and `DBCC DROPCLEANBUFFERS`** - when **Clear Buffer Pool** is on, which empties the buffer pool for the whole server
- **`DBCC TRACEON(652)`** - when **Disable Read-Ahead** is on, for the query's session only
- **`fn_dblog`** - to read the log records of a traced data modification, which runs in a transaction that is rolled back

[Full Trace](/docs/user-guide/query/FullTrace) also needs administrator rights on the machine, since it attaches a recorder to the SQL Server service.
