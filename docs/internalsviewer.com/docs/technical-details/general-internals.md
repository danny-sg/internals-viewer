# Undocumented DBCC Commands

List all DBCC commands:

```sql
DBCC TRACEON(2588)
DBCC HELP ('?')

-- Then DBCC HELP for the command syntax, e.g. 
DBCC HELP ('READPAGE')
```

Reference - https://www.sqlskills.com/blogs/paul/dbcc-writepage/

# System Base Tables

See - https://learn.microsoft.com/en-us/sql/relational-databases/system-tables/system-base-tables

If you connect to SQL Server using a [Dedicated Administrator Connection (DAC)](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/diagnostic-connection-for-database-administrators) you can query these tables.

To connect on a DAC connection prefix the server name with `admin:`.

Only one DAC connection is allowed at a time.

# sp_helptext

The system stored procedures, views, DMVs etc. have underlying SQL that can be viewed using `sp_helptext`.

For example `sys.objects` is a view based on a hidden base table, `sys.objects$`.