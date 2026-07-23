# How the database is loaded

A SQL Server database describes itself. Given the raw pages, there is enough information inside the database to work out every table, column, index, and allocation - because that is exactly what SQL Server itself does. Internals Viewer implements enough of the storage engine's read path to bootstrap a complete picture of a database starting from a single well-known page.

Internals Viewer boots the database as follows:

## Reading pages

Everything is built on one primitive: _read page (file:page) and return its 8192 bytes_. There are two implementations, one per connection type.

**Data file connections** read the file directly. Pages are stored in the file in order, so a page is located at `Page Id × 8192` and read with an ordinary file seek and read. The file has to be detached or offline - an attached file is locked exclusively by the SQL Server process.

**SQL Server connections** can't touch the file (SQL Server has it locked), so pages are read through the server itself using `DBCC PAGE`:

```SQL
DBCC PAGE(<database>, <file id>, <page id>, 2) WITH TABLERESULTS
```

Dump option `2` returns the full page as a formatted hex dump. Internals Viewer parses the dump text back into the original 8192 bytes. This is why a server connection requires `sysadmin` - `DBCC PAGE` is an undocumented, admin-only command.

Everything above the page reader is identical - the same parsing and decoding runs whether the bytes came from a file or from a hex dump.

> [!NOTE]
> The Start page also offers backup files as a connection type. Reading pages directly out of a full backup is still work in progress.

## Starting point - the boot page

The bootstrap starts at the **boot page**, which is always page `(1:9)` in every database. Among the database-wide metadata it holds is a field called `dbi_firstSysIndexes` - the page address of the first page of a system base table called `sys.sysallocunits`.

That one address is the thread the whole database hangs off.

## The system base tables

SQL Server stores its own metadata in a set of hidden _system base tables_. Every catalog view you normally query - `sys.objects`, `sys.columns`, `sys.indexes` - is a view built over these tables. They can't be queried directly without a DAC (Dedicated Admin Connection), but they are just tables: ordinary FixedVar records in ordinary data pages. Internals Viewer reads them with the same record decoder the Page Viewer uses.

`sys.sysallocunits` is read first, starting from the page the boot page pointed to and following the page linkage. It lists every allocation unit in the database with its entry points - Root Page, First Page, and First IAM Page.

The base tables have fixed, well-known object and index ids, and an allocation unit id is derived from the object id and index id. So once `sys.sysallocunits` is loaded, the first page of every other base table can be looked up in it, and each is read in turn:

| Base table          | Contents                                             | Closest catalog view                     |
| ------------------- | ---------------------------------------------------- | ---------------------------------------- |
| `sys.sysallocunits` | Allocation units and their entry points              | `sys.system_internals_allocation_units`  |
| `sys.sysrowsets`    | Rowsets/partitions                                   | `sys.partitions`                         |
| `sys.sysschobjs`    | Objects - tables, views, procedures                  | `sys.objects`                            |
| `sys.sysrscols`     | Physical column layout per rowset - offsets, lengths | `sys.system_internals_partition_columns` |
| `sys.syscolpars`    | Column definitions                                   | `sys.columns`                            |
| `sys.sysclsobjs`    | Classified entities, e.g. schemas                    | -                                        |
| `sys.sysidxstats`   | Indexes                                              | `sys.indexes`                            |
| `sys.sysiscols`     | Index columns                                        | `sys.index_columns`                      |
| `sys.sysprufiles`   | Database files                                       | `sys.database_files`                     |

Together these answer every question the rest of the application asks: what objects exist, what their names are, which columns they have and at what offsets in the record, what indexes exist, and where everything starts.

There is circularity here: the metadata that describes how to decode records is itself stored as records, which have to be decoded to read it. The bootstrap works because the base tables' own structures are fixed and known ahead of time. As mentioned in [Background](/docs/user-guide/background.md), these base tables are some of the most ancient parts of the database. If anything changes in these tables it will be linked to very core functionality changes in the engine.

::: details Verifying with SQL
The equivalent of the first step can be seen on a live database (the undocumented `sys.fn_PhysLocFormatter` formats the binary page addresses):

```SQL
SELECT allocation_unit_id
      ,sys.fn_PhysLocFormatter(first_page)     AS first_page
      ,sys.fn_PhysLocFormatter(root_page)      AS root_page
      ,sys.fn_PhysLocFormatter(first_iam_page) AS first_iam_page
FROM   sys.system_internals_allocation_units
```

:::

## Building the database picture

With the metadata loaded, the database model is assembled:

1. **Allocation units and files** are built from the metadata - names resolved, columns mapped to offsets, entry points decoded from their binary form.

2. **File allocation bitmaps** are loaded for each data file - the GAM, SGAM, DCM, and BCM chains. Each is a chain of bitmap pages at fixed intervals through the file (one page per ~4 GB), read and combined into one bitmap per file.

3. **PFS chains** are loaded - the first PFS is page `(1:1)` and they repeat every 8088 pages. These provide the per-page allocation status and fullness shown by the PFS overlay.

4. **IAM chains** are loaded for every allocation unit, starting from its First IAM Page and following the chain via the page header's Next Page pointer. Each allocation unit's chain is independent, so they are loaded in parallel (up to 16 at a time).

The allocation map you see when a database opens is a direct render of step 4 - every object's IAM chain drawn over the file, coloured per index, with the PFS data from step 3 available as an overlay.

Refreshing the database repeats the metadata and allocation loading against the current state of the database.

## In the source

The key classes, for reading along in the [repository](https://github.com/danny-sg/internals-viewer):

- `InternalsViewer.Internals/Readers/Pages/DataFilePageReader.cs` and `QueryPageReader.cs` - the two page readers
- `InternalsViewer.Internals/Services/Loaders/Engine/DatabaseService.cs` - orchestrates the load
- `InternalsViewer.Internals/Services/Loaders/Engine/MetadataLoader.cs` - the base table bootstrap
- `InternalsViewer.Internals/Metadata/Internals/Tables/` - the base table record definitions
