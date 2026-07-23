---
outline: deep
---

# Getting started

## Requirements

Internals Viewer will run on Windows 10/11 (version 17763.0 or higher).

SQL Server 2019-2025 is required when connecting to a SQL Server database or file.

`sysadmin` permissions are required for a SQL Server connection, see [Permissions](/docs/user-guide/permissions).

## Installation

The easiest way to install Internals Viewer is to get it from the Microsoft Store.

<a href="https://get.microsoft.com/installer/download/9MSW42CQMK2V?referrer=appbadge" target="_self" >
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

To install manually see [Installation](/docs/user-guide/installation.md).

## Connecting

When you open the application the Start page will bring up the different options to connect - a live SQL Server instance, a detached/offline data file, or a database backup.

![Start page](/docs/tutorial/images/screenshots/Start_page.png)

The Start page also lists recent connections - click one to reconnect. Passwords are not stored, so a SQL Server Authentication connection will prompt for the password again.

### SQL Server

To connect to a SQL Server instance:

- Click on Connect to SQL Server
- Set Instance Name to the name of the SQL Server
- Choose the Authentication type - Windows Authentication, SQL Server Authentication, or Active Directory Password
- For Database either type in the name of the database or expand the drop down list to see a list of databases on the server
- Click Connect

![Connect to SQL Server](/docs/tutorial/images/screenshots/Connect_sql_server.png)

### Data file

A database can also be opened directly from its MDF data file, without a SQL Server instance. The file must be detached from SQL Server or the database set offline - an attached, online file is locked exclusively by the SQL Server process.

Click **Data file**, browse to the MDF file, and click **Open**. No permissions are needed beyond read access to the file, and everything is read-only - see [How the database is loaded](/docs/deep-dives/loading-a-database) for how this works.

![Connect to a database file](/docs/tutorial/images/screenshots/Connect_database_file.png)

Note the [Query](/docs/user-guide/query) view needs a live connection to trace queries, so it is only available for SQL Server connections.

### Backup file

Opening a database directly from a full backup is a work in progress and not yet available.
