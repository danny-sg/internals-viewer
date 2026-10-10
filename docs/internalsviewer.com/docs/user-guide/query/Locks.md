# Locks

The Lock band shows [locks](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide) acquired and released over the life of the query - how SQL Server protects data being read and modified from conflicting changes by other transactions. Locks are only captured when enabled - see the [Events menu](/docs/user-guide/query#events-menu).

The band has a row for each lock category the query took, with the most exclusive at the top - so an escalation to a coarser lock steps up - and intent locks in rows of their own below the real locks. Schema locks are just another category, left out by default because they are held for a large part of the query's lifetime and would dominate the band.

Each row is a bar chart of how many locks of that kind were held at each moment, so it reads the same whether two locks overlapped or thousands did.

## Categories and colours

The [lock modes](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide#lock-modes) are grouped into categories:

| Category | Lock modes | Colour |
| --- | --- | --- |
| Read | S, IS | Green |
| Update | U, IU, SIU | Amber |
| Write / Exclusive | X, IX, SIX, UIX | Red |
| Schema | SCH_S, SCH_M | Purple |
| Range | RS_*, RI_*, RX_* | Blue |
| Bulk | BU | Teal |

These colours are used consistently wherever lock state is shown - including the border drawn around pages on the [Allocations](/docs/user-guide/query/Allocations) pane. Intent lock modes (the `I`-prefixed and `SI`/`UI` modes) are dimmed relative to their full counterpart.

## Lock escalation

When a query holds too many fine-grained locks, SQL Server [escalates](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide#lock-escalation) them to a single coarser lock on the whole object. A lock escalation is marked as a discrete event on the timeline - a solid vertical line at the point of escalation, with a tooltip describing the change, e.g. "Lock escalation: X (Exclusive) on Object, replacing 6249 lock(s)":

![Lock escalation marker on the timeline](/docs/user-guide/images/query-lock-escalation-cropped.png)
