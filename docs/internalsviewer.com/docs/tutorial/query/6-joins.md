# Joins

SQL Server has three [physical join operators](https://learn.microsoft.com/en-us/sql/relational-databases/performance/joins) - **Nested Loops**, **Merge Join**, and **Hash Match** - and they drive the storage engine in completely different ways. On this page we'll trace the same logical join hinted three ways and compare their access patterns side by side:

- Loop join - a seek per row
- Merge join - two sorted streams zipped together
- Hash join - build then probe

## Setting the scene

All three queries join the heap to the clustered table on `h.NumberField = c.Id`. Both sides of the key are indexed - `IX_HeapTable_NumberField` from Part 3 and the clustered `PK_ClusteredTable` - which is what gives the optimizer (and our hints) all three strategies to work with.

First, top up the data. The heap's keys so far only cover 100 to 1100 - a thin band at the very start of the clustered table, so the joins would only ever touch its first few pages. Spreading the keys across the whole range makes each join's access pattern play out across the entire index:

```SQL
INSERT INTO dbo.HeapTable
        (NumberField, TextField, FixedTextField)
SELECT  Id
       ,CONCAT('Join row ', Id)
       ,LEFT(CONCAT('Row ', Id), 10)
FROM    dbo.ClusteredTable
WHERE   Id % 20 = 0
GO
```

This adds 5,000 rows whose keys are every 20th `Id` - evenly spaced from one end of the clustered index to the other - taking the heap to around 6,000 rows.

Keep **Clear Buffer Pool** and **Disable Read-Ahead** on so each join's access pattern shows clearly in the Read lane.

## Loop join - seek per row

```SQL
SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER LOOP JOIN dbo.ClusteredTable c
         ON c.Id = h.NumberField
```

Nested Loops takes one row at a time from the outer input (the heap, read through `IX_HeapTable_NumberField` - around 6,000 rows) and, for each one, seeks into the inner input (the clustered index) for the match.

![Loop join](/docs/tutorial/images/screenshots/Query_join_loop.png)

What to watch:

- The Read lane shows the signature pattern - a rapid, repeating drumbeat of single-page reads, one seek per outer row
- Right-click `PK_ClusteredTable` in the Plan lane and open the Index view: the seeks land all over the leaf level, one root-to-leaf path at a time, in whatever order the heap supplies the keys
- Nested Loops is fully streaming - rows flow out from the very first match, no waiting

## Merge join - two sorted streams

```SQL
SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER MERGE JOIN dbo.ClusteredTable c
         ON c.Id = h.NumberField
```

Merge Join requires both inputs sorted on the join key, then zips them together in a single interleaved pass. Both sides can supply the order for free: `IX_HeapTable_NumberField` is sorted on `NumberField` and the clustered index is sorted on `Id`.

![Merge join](/docs/tutorial/images/screenshots/Query_join_merge.png)

What to watch:

- Two ordered scans running together - the Read lane shows steady in-order reads from both indexes, each page touched exactly once
- Open the Index view for either index: the pages light up strictly left to right, both indexes swept in step with each other
- Like Nested Loops it streams - matches are emitted as the two streams line up - but with sequential access instead of repeated seeks

> [!NOTE]
> The optimizer may decide to scan the heap and add a **Sort** operator instead of using `IX_HeapTable_NumberField` - for a small table, sorting a few thousand rows can cost less than the ordered index scan. If the plan shows a Sort, the merge's second input is coming from memory and its reads all happen up front (a blocking phase, as seen in [Using the Query view](/docs/tutorial/query/1-using-the-query-view)). To force the pre-sorted index and see the pure two-stream merge, add an index hint: `FROM dbo.HeapTable h WITH (INDEX(IX_HeapTable_NumberField))`.

## Hash join - build then probe

```SQL
SELECT c.Id
      ,c.TextField
FROM   dbo.HeapTable h
       INNER HASH JOIN dbo.ClusteredTable c
         ON c.Id = h.NumberField
```

Hash Match reads the entire smaller input (the heap) first and builds a hash table from it, then streams the larger input through the hash table looking for matches.

![Hash join](/docs/tutorial/images/screenshots/Query_join_hash.png)

What to watch:

- The two phases are visible in the Plan lane. During the **build** phase the heap scan runs while the Hash Match bar sits in its dimmed consuming state and nothing flows to the SELECT. Then the **probe** phase starts and the probe input's scan streams through
- The Read lane shows the same story - a short burst of reads (the heap), a pause, then the long steady scan of the probe input
- Unlike the other two joins there are no seeks at all - both inputs are consumed by scans, and the matching happens in memory

> [!NOTE]
> Look closely at the probe input in the plan: it scans `IX_ClusteredTable_TextField`, not the clustered index. The non-clustered index covers the two columns the query needs (`TextField` as the key, `Id` as the clustering key) and is smaller than the table, so the optimizer reads it instead - fewer pages, same rows.

## Comparing them

| | Loop | Merge | Hash |
| --- | ---- | ----- | ---- |
| Inner table access | One seek per outer row | One ordered scan | One scan |
| Read lane pattern | Repeating single-page reads | Two interleaved sequential streams | Burst, pause, long scan |
| Blocking? | Streams from first row | Streams | Blocks during build phase |
| Needs | An index to seek on | Both inputs sorted on the key | Memory for the hash table |

Finally, remove the hint and trace the query once more - the plan shows which strategy the optimizer picks when left to its own devices, and by now the timeline will tell you why.

Next: [Log Records](/docs/tutorial/query/7-log-records)
