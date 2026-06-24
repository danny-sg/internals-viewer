# Welcome to Internals Viewer

Internals Viewer is a tool for examining how a SQL Server database is physically stored
on disk — the pages, extents, allocation maps, indexes and records the engine works with
beneath the level of normal queries.

It is intended for a range of users:

- Anyone wanting to understand what a database looks like at the storage level.
- DBAs, developers and engineers investigating storage, indexing, fragmentation and
  query behaviour at the byte level.

> **Concept panels**
> Where a section introduces a storage concept, it includes a short explanation in a
> panel like this one. If terms such as *page*, *extent* or *allocation* are unfamiliar,
> the [SQL Server internals primer](Internals.md) covers them from the ground up.

> **Image placeholder — start-page**
> The Start page with the three connect tiles and Recent connections.
>
> _To be added. Source: `temp-screenshots/Start_page.png`; crop: trim the OS title bar (top ~44px), keep full width._

## Connecting to a database

The Start page is where a session begins. Internals Viewer requires a database to
examine, so the first step is to connect. There are three connection methods, shown as
tiles:

| Tile | What it does | When to use it |
| --- | --- | --- |
| **Connect to SQL Server** | Connects to a live SQL Server instance. | The usual choice — examine any database on a reachable server. |
| **Data file** | Opens a detached/offline `.MDF` database file directly. | When you have a database file but no running server. |
| **Database backup file** | Opens a database from a full backup. | Not yet available. |

The navigation menu on the left mirrors these options (**SQL Server**, **Database File**,
**Database Backup File**), so the connection method can be changed at any time.

See [Connections](Connections.md) for the details of each method.

## Recent connections

After a successful connection, the database appears under **Recent connections** at the
bottom of the Start page. Selecting one reconnects without re-entering the details.

> **Warning: choose the target carefully**
> Internals Viewer reads a large amount of low-level structure and is intended for
> investigation and learning rather than production monitoring. Use a development or
> sample database (the screenshots in this guide use Microsoft's **AdventureWorks**), and
> avoid running it against busy production servers or very large databases. See
> [Connections](Connections.md) for details.

## Where to go next

- [Connections](Connections.md) — the three connection methods and their requirements.
- [Allocations / Database View](Allocations.md) — the map of everything stored in the database.
- [Pages](Pages.md) — the 8 KB building block of all storage, decoded.
- [Indexes](Indexes.md) — the physical layout of an index as a tree.
- [Query](Query.md) — run a query and observe how SQL Server executes it.
