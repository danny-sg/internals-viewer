# Indexes and data

A seek walks root to leaf on the index the plan names - but what the leaf level holds depends on the kind of index, and it decides whether the query is finished when the seek lands or has more work to do. There are four cases, and each has a distinct signature on the timeline.

Keep **Clear Buffer Pool** and **Disable Read-Ahead** on for these queries, as in [Scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks).

## Clustered index - the leaf is the data

In a clustered index the leaf level _is_ the table, stored in key order - the seek in [Scans vs seeks](/docs/tutorial/query/4-scans-vs-seeks) is this case. Landing on the leaf page is landing on the row, so `SELECT *` is satisfied on the spot and the query ends with the seek.

## Non-clustered index - key lookup

A non-clustered index is a separate, smaller B-Tree whose leaf records hold the index key plus the clustering key pointing back to the table (Part 3 built `IX_ClusteredTable_TextField` and looked at exactly these records). Find the same row as before, but through the non-clustered index:

```SQL
SELECT *
FROM   dbo.ClusteredTable
WHERE  TextField = 'This is row 54321'
```

The plan now has two operators: an Index Seek on `IX_ClusteredTable_TextField` and a **Key Lookup** on `PK_ClusteredTable`. The non-clustered leaf record contains `TextField` and the clustering key `Id` - but not `CreatedDate`, so the engine takes `Id` 54321 from the leaf record and runs a second root-to-leaf seek into the clustered index to fetch the rest of the row. Open both indexes from the Plan lane and two trees light up, one root-to-leaf path each.

That second seek is paid _per matching row_ - a predicate matching thousands of rows pays thousands of key lookups, which is why the optimizer abandons the non-clustered index for a scan when it expects too many matches.

::: info The uniquifier
The clustering key only works as a row locator because it identifies exactly one row. If the clustered index is _non-unique_, SQL Server makes it unique itself by adding a hidden **uniquifier** to duplicate keys:

```SQL
CREATE TABLE dbo.DuplicateKeyTable
(
    Category  INT          NOT NULL
   ,TextField VARCHAR(100) NOT NULL
)
GO

CREATE CLUSTERED INDEX IX_DuplicateKeyTable_Category ON dbo.DuplicateKeyTable (Category)
GO

INSERT INTO dbo.DuplicateKeyTable
        (Category, TextField)
VALUES  (1, 'First row in category 1')
       ,(1, 'Second row in category 1')
       ,(1, 'Third row in category 1')
       ,(2, 'Only row in category 2')
GO
```

Open the table's data page in the Page Viewer: the second and third `Category` 1 rows carry a `Uniquifier` value, while the first occurrence and the `Category` 2 row store nothing - it's only added to actual duplicates. Non-clustered indexes on this table use `Category` _plus_ the uniquifier as the row locator.
:::

## Covering non-clustered index - no lookup

Now ask only for columns the non-clustered index already holds:

```SQL
SELECT Id
      ,TextField
FROM   dbo.ClusteredTable
WHERE  TextField = 'This is row 54321'
```

The Key Lookup disappears. `TextField` is the index key and `Id` is the clustering key carried in every leaf record, so the leaf record contains everything the query needs - the index _covers_ the query and the clustered index is never touched. The whole query is a single root-to-leaf path down the smaller tree.

This is the point of `INCLUDE` columns: `CREATE INDEX ... ON dbo.ClusteredTable (TextField) INCLUDE (CreatedDate)` stores `CreatedDate` in the leaf records too, making the index cover the first query without widening the keys above the leaf.

## Non-clustered index on a heap - RID lookup

A heap has no clustering key to point to, so its non-clustered indexes store a RID - the `(File Id:Page Id:Slot Id)` physical address seen in Part 3:

```SQL
SELECT *
FROM   dbo.HeapTable
WHERE  NumberField = 500
```

The plan is an Index Seek on `IX_HeapTable_NumberField` plus a **RID Lookup** on the heap. Unlike a key lookup there is no second B-Tree to walk - the RID goes straight to the page and slot, so each lookup is a single page read.

## The four cases

| Index | Leaf level holds | After the seek |
| --- | ---- | ---- |
| Clustered | The rows themselves | Nothing - the data is already in hand |
| Non-clustered, non-covering | Key + clustering key | Key Lookup - a seek into the clustered index per row |
| Non-clustered, covering | Key + clustering key + `INCLUDE` columns - everything the query needs | Nothing - the leaf record answers the query |
| Non-clustered on a heap | Key + RID | RID Lookup - one page read straight to the page and slot |

Next: [Joins](/docs/tutorial/query/6-joins)
