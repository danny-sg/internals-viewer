# Permissions

`sysadmin` permissions are required for a SQL Server connection. 

Permissions are not required to read .mdf or .bak files other than file read permissions.

Internals Viewer uses the `DBCC PAGE` command to connect to online databases. This is the only command used as everything is done from the page data. `DBCC PAGE` requires `syadmin` permissions due to the nature of the command - it can read anything in the database regardless of permissions.
