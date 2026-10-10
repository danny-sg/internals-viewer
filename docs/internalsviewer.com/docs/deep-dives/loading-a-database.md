# How the database is loaded

A SQL Server database describes itself. Given the raw pages, there is enough information inside the database to work out every table, column, index, and allocation - because that is exactly what SQL Server itself does. Internals Viewer implements enough of the storage engine's read path to bootstrap a complete picture of a database starting from a single well-known page.

Internals Viewer boots the database as follows:

## Reading pages

Everything is built on one primitive: _read page (file:page) and return its 8192 bytes_. There are three implementations, one per connection type.

**Data file connections** read the files directly. Pages are stored in a data file in order, so a page is located at `Page Id × 8192` and read with an ordinary file seek and read. Each page is routed by its File Id to the `.mdf` or to one of the database's `.ndf` files, which are found as the metadata is loaded (see [Secondary data files](#secondary-data-files) below). The files have to be detached or offline - SQL Server holds the files of an online database open.

**SQL Server connections** can't touch the files (SQL Server has them open), so pages are read through the server itself using `DBCC PAGE`:

```SQL
EXEC ('DBCC PAGE([<database>], <file id>, <page id>, 2) WITH TABLERESULTS')
WITH RESULT SETS
(
    (
        Unused0 NVARCHAR(4000)
       ,Unused1 NVARCHAR(4000)
       ,Unused2 NVARCHAR(4000)
       ,Value   NVARCHAR(MAX)
    )
);
```

Dump option `2` returns the full page as a formatted hex dump. Internals Viewer parses the dump text back into the original 8192 bytes. The `WITH RESULT SETS` wrapper fixes the shape and types of the columns, so the dump always comes back the same way. This is why a server connection requires `sysadmin` - `DBCC PAGE` is an undocumented, admin-only command.

**Backup connections** read pages out of a full backup file, without restoring it. A backup is not laid out like a data file, so it is indexed first - see [Backups](#backups) below.

Everything above the page reader is identical - the same parsing and decoding runs whether the bytes came from a file, a hex dump or a backup.

## Starting point - the boot page

Before anything is read, the page reader is initialised - for a server it checks the version and whether the login is `sysadmin`, for a data file it opens the primary file, and for a backup it reads and indexes the backup.

The bootstrap then starts at the **boot page**, which is always page `(1:9)` in every database. Among the database-wide metadata it holds are the database id and a field called `dbi_firstSysIndexes` - the page address of the first page of a system base table called `sys.sysallocunits`.

That one address is the thread the whole database hangs off.

## The system base tables

SQL Server stores its own metadata in a set of hidden _system base tables_. Every catalog view you normally query - `sys.objects`, `sys.columns`, `sys.indexes` - is a view built over these tables. They can't be queried directly without a DAC (Dedicated Admin Connection), but they are just tables: ordinary FixedVar records in ordinary data pages. Internals Viewer reads them with the same record decoder the Page Viewer uses.

`sys.sysallocunits` is read first, starting from the page the boot page pointed to and following the page linkage. It lists every allocation unit in the database with its entry points - Root Page, First Page, and First IAM Page.

The base tables have fixed, well-known object and index ids, and an allocation unit id is derived from the object id and index id. So once `sys.sysallocunits` is loaded, the first page of every other base table can be looked up in it, and each is read in turn:

| Base table          | Contents                                             | Closest catalog view                     |
| ------------------- | ---------------------------------------------------- | ---------------------------------------- |
| `sys.sysallocunits` | Allocation units and their entry points              | `sys.system_internals_allocation_units`  |
| `sys.sysprufiles`   | Database files                                       | `sys.database_files`                     |
| `sys.sysrowsets`    | Rowsets/partitions                                   | `sys.partitions`                         |
| `sys.sysschobjs`    | Objects - tables, views, procedures                  | `sys.objects`                            |
| `sys.sysrscols`     | Physical column layout per rowset - offsets, lengths | `sys.system_internals_partition_columns` |
| `sys.syscolpars`    | Column definitions                                   | `sys.columns`                            |
| `sys.sysclsobjs`    | Classified entities, e.g. schemas                    | -                                        |
| `sys.sysidxstats`   | Indexes                                              | `sys.indexes`                            |
| `sys.sysiscols`     | Index columns                                        | `sys.index_columns`                      |

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

### Secondary data files

The order matters for one table. `sys.sysprufiles` is read second, straight after `sys.sysallocunits`, and the database's other data files are found before any other base table is read. The base tables live in the PRIMARY filegroup, and a filegroup can span several files, so the next page of a base table may well be in file 3.

For a data file connection, each secondary data file is looked for at the path recorded in `sys.sysprufiles`, then under the same name in the folder the `.mdf` was opened from. A candidate is only used if its page 0 is a file header page for the file id it should be, so a stray copy of another database's file is not picked up. Log and FILESTREAM files are skipped. For a backup, the check is that the backup holds pages for every data file.

## Building the database picture

With the metadata loaded, the database model is assembled:

1. **Allocation units and files** are built from the metadata - names resolved, columns mapped to offsets, entry points decoded from their binary form.

2. **File allocation bitmaps** are loaded for each data file - the GAM, SGAM, DCM, and BCM chains. Each is a chain of bitmap pages at fixed intervals through the file (one page per ~4 GB), read and combined into one bitmap per file.

3. **PFS chains** are loaded - the first PFS is page `(1:1)` and they repeat every 8088 pages. These provide the per-page allocation status and fullness shown by the PFS overlay.

4. **IAM chains** are loaded for every allocation unit, starting from its First IAM Page and following the chain via the page header's Next Page pointer. Each allocation unit's chain is independent, so they are loaded in parallel (up to 16 at a time).

5. **Columnstore delta stores** are matched to their row groups, so a columnstore index's open row groups appear under it in the allocation map.

The allocation map you see when a database opens is a direct render of step 4 - every object's IAM chain drawn over the file, coloured per index, with the PFS data from step 3 available as an overlay.

Refreshing the database repeats the whole load, page reader included, against the current state of the database.

## Backups

A full backup holds every allocated page of the database, but not in a form that can be read by address. It is a stream of page images in Microsoft Tape Format (MTF), written by a backup that runs while the database is in use. Opening one means working out where in the backup each page is, once, then reading pages from there.

### The MTF container

An uncompressed `.bak` is an MTF stream of descriptor blocks - `TAPE` first, then `SSET` (the start of a backup set), `VOLB`, SQL Server's own `MSCI` (configuration), `MSDA` (data), `MSTL` (log) and `MSLS` blocks, then `ESET` at the end of the set. Each block has a 52-byte common header followed by streams, each with a 22-byte header giving its id, length and whether it is compressed or encrypted.

The pages are in `MQDA` streams inside the `MSDA` blocks: a 2-byte prefix, then whole 8192-byte page images. Only the data blocks matter for reading pages. The log in the `MSTL` blocks is not applied, so pages are shown exactly as they were copied.

Only the first backup set in a file is read - parsing stops at the first `ESET`. A database with FILESTREAM or memory-optimised data adds sections that have no length field. These are skipped by scanning forward for the next known block tag, and a candidate is only accepted if its position matches the one its own header claims relative to the `SSET` block. Payload bytes cannot fake that.

### Finding the pages

Pages in a backup are positional rather than addressed. Within a run, consecutive slots are consecutive pages of the same file, and the run jumps where the backup skipped unallocated extents. So the page map is built by reading the 96-byte header of each page image, 128 pages (1 MB) at a time:

- **A page with an address** - its file id and page id are read from the header
- **An empty page** - a zeroed page inside an allocated extent carries on the current run, since it is real file content
- **A filler page** - type 101, which pads streams to 1 MB, ends the run

Runs of consecutive pages at consecutive offsets are stored as one entry - file id, first page, page count, stripe and offset - so the map stays tiny even for a large database. A lookup is a binary search on the file's runs and one 8 KB read.

A full backup ends with a second, small data block that writes the system pages - the file header, PFS, GAM and DCM - again, because they changed while the backup ran. The data blocks are processed in order across every stripe, and later images win, so the map points at the final copy.

A page the backup does not hold - one that was not allocated when the backup was taken - is reported as not in the backup rather than read as zeros.

### Striped backups

A backup striped across several files holds a `RAID` stream in each file's `TAPE` block, giving the media set id, the number of files in the set and the file's place in it. Before anything is read the files are checked - every file must carry the same media set id, each place in the set must appear exactly once (two copies of one place are a mirror, and only one is wanted), and the count must match - and then they are put in order. Each run in the page map records which stripe it is in.

### Compressed backups

A compressed backup is not MTF at the top level. It starts with `MSSQLBAK` and a 480-byte header whose algorithm field says how it was compressed, followed by a chain of chunks, each with a 28-byte header - a marker for compressed or raw, the uncompressed size, the payload size and a checksum. Underneath the chunks is the same MTF stream as an uncompressed backup, so everything above reads a compressed backup as if it were not.

- **MS_XPRESS**, the default algorithm, is LZ77 with Huffman coding (MS-XCA). Two rules differ in this container: symbol 256 is an ordinary match rather than the end of the stream, and decoding stops at the size the chunk header declares. The 64 KB match window carries on across chunks, so a chunk cannot be decoded without the one before it.
- **ZSTD**, from SQL Server 2025, writes every chunk as an independent zstd frame, so any chunk can be decoded on its own.

A chunk that cannot be decoded is zero-filled, so the stream stays aligned and the page map can carry on past it.

Opening a compressed backup decodes the whole file once to index it, keeping each chunk's offset and a checkpoint every 4 MB of output - with the last 64 KB of history for Xpress. A read then decodes forward from the nearest checkpoint into a 16 MB window, which doubles as the cache. Between indexing and building the page map, a compressed backup is decoded about twice while it opens, and its reads are serialised, so it opens more slowly than an uncompressed one.

### What is not read

- **Encrypted backups** - a stream marked as encrypted stops the backup opening.
- **Later backup sets** - only the first set in a file.
- **The backup's log** - the pages are shown as copied.
- **Differential backups** - these index without complaint, but only hold the pages changed since the last full backup, so the database cannot be browsed from one alone.

## In the source

The key classes, for reading along in the [repository](https://github.com/danny-sg/internals-viewer):

- `InternalsViewer.Internals/Readers/Pages/DataFilePageReader.cs` and `QueryPageReader.cs` - the data file and server page readers
- `InternalsViewer.Connection.BackupFile/Reader/BackupPageReader.cs` - the backup page reader
- `InternalsViewer.Connection.BackupFile/Mtf/` - the MTF block and stream parser
- `InternalsViewer.Connection.BackupFile/Media/MediaSetReader.cs` - checking and ordering the files of a striped backup
- `InternalsViewer.Connection.BackupFile/Mapping/PageMapper.cs` - building the page map
- `InternalsViewer.Connection.BackupFile/Compression/` - compressed backups: the chunk chain, the Xpress and ZSTD decoders and the checkpointed reader
- `InternalsViewer.Internals/Services/Loaders/Engine/DatabaseService.cs` - orchestrates the load
- `InternalsViewer.Internals/Services/Loaders/Engine/MetadataLoader.cs` - the base table bootstrap
- `InternalsViewer.Internals/Metadata/Internals/Tables/` - the base table record definitions
