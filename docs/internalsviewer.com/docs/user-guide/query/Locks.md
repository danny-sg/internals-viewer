# Locks

The Lock band shows [locks](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide) acquired and released over the life of the query - how SQL Server protects data being read and modified from conflicting changes by other transactions. Locks are only captured when enabled - see the [Events menu](/docs/user-guide/query#events-menu).

Locks are bucketed per object, and each object's locks are banded by category, split into **non-schema** and **schema** locks - schema locks are banded separately so they don't dominate the view, since they are typically held for longer and across a broader scope than row/page/key locks.

Within a band, locks are drawn as a histogram - a bar chart showing the volume of locks held in that category over time.

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
