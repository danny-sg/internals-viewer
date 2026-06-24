# Connections

Internals Viewer can examine a database in three ways, selected from the **Start** page
tiles or the navigation menu on the left. Each opens a connected database in its own tab
at the top of the window.

> **Concept: what a connection provides**
> Unlike a normal query tool, Internals Viewer reads a database's *physical structure* —
> the pages, allocation maps and index trees — not just query results. A connection is
> the route it uses to reach those raw bytes, whether from a live server or a file on
> disk.

## Connect to SQL Server

Connects Internals Viewer to a live SQL Server instance and provides access to the
databases on it.

> **Image placeholder — connect-sql-server**
> The SQL Server connection form (Instance Name, Authentication, User Id/Password, Database, Connect button).
>
> _To be added. Source: `temp-screenshots/Connect_sql_server.png`; crop: trim OS title bar, keep full width._

Fields:

- **Instance Name** — the name or network address of the SQL Server instance (e.g. `localhost`).
- **Authentication** — the sign-in method. *Active Directory Integrated* uses the current
  Windows account; other methods enable the **User Id** and **Password** fields.
- **Database** — the database on the instance to open.

Then select **Connect**.

> **Warning: requires the `sysadmin` server role**
> Reading raw pages and allocation structures requires elevated permissions, so the login
> used must be a member of the server-level **sysadmin** role. This is a further reason to
> connect to development or test servers rather than production.

> **Warning: not for production or very large databases**
> Some views decode a large amount of structure up front (for example, the
> [Index](Indexes.md) view reads every non-leaf page), which is slow and adds load on a
> large or busy database. Use sample databases such as **AdventureWorks** or dedicated dev
> databases.

## Data file

Opens a SQL Server `.MDF` data file (the primary database file) directly, without a
running server.

> **Image placeholder — connect-data-file**
> The MDF File form (Filename + Browse, Open button).
>
> _To be added. Source: `temp-screenshots/Connect_database_file.png`; crop: trim OS title bar, keep full width._

Select **Browse** to choose the file, then **Open**. Internals Viewer uses its own
built-in engine to boot the database from the file and read it in its entirety.

> **Concept: how this differs from a server connection**
> With a live server, SQL Server supplies the pages to Internals Viewer. When opening an
> `.MDF` file there is no server — Internals Viewer reads and interprets the file format
> itself. This makes it suitable for forensic or offline investigation.

Requirements and limitations:

- The file must be **detached from SQL Server or set offline**. SQL Server holds an
  exclusive lock on the files of an attached, online database, so the file cannot be read
  while in use.
- A file connection is offline by definition — with no server, server-only features (such
  as the [Query](Query.md) view) are unavailable.

## Database backup file

Opens a database directly from a full database backup.

> **Note:** This option is reserved for future functionality and is not yet available.

## Working with multiple connections

Several connections can be open at once — to different databases, or a mix of servers and
files. Each opens in its own tab along the top of the window. Switch between them by
selecting a tab, and close one with the **✕** on the tab.

## Comparison

| | SQL Server | Data file | Backup file |
| --- | --- | --- | --- |
| Source | Live instance | `.MDF` file | Backup file |
| Requires a running server | Yes | No | No |
| Permissions | `sysadmin` | File access | — |
| [Query](Query.md) view available | Yes | No | — |
| Status | Available | Available | Not yet available |
