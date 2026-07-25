# Backup File Connection Type

> Note
> This is from investigations and piecing together various sources of information - it's not definitive or guarantee to be correct, just 
  what has been discovered so far.

SQL Server full backup files are still essentially a store of the page data.

Not 100% sure on this, but it seems in a backup the pages are sequenced per object, probably via an IAM scan. The MDF/LDF files are a 
physical layout where page address represent file/offset in file (Page Id * 8192), whereas backups have the same pages but without gaps or
fragmentation from the physical file so there is a space saving element to a backup.

Restoring a database from a file reads the pages, gets the page address from each header, and writes it at that location to restore the 
physical layout.

Backup files contain data and log sections. The Internals Viewer connection type does not use the log records, but a database restore would
include these too.

The Internals Viewer Backup File Connection Type is essentially a translation from the layout in the backup file to the equivalent address
in a data file.

## File format

Uncompressed backups use the [MTF (Microsoft Tape Format) format](https://en.wikipedia.org/wiki/Microsoft_Tape_Format), which is relatively
ancient backup format (with references to DOS, Windows 95 and OS2).

MTF is block based. Blocks will have a header, and embedded data that depends on the type of block. Parsing reads block headers, gets 
information about that block including the block length, then moves to the next block until it get to the end of the file or an end block.

## Compressed backups

Compressed backups are just a compressed version of the MTF data with the same block structures. The payload is compressed with a header 
to describe the compression.

Pre-SQL Server 2025 backups use the [Microsoft Xpress Compression Algorithm](https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-XCA/%5bMS-XCA%5d.pdf), 
specifically the LZ77+Huffman implementation.

2025+ backups can additionally use the [ZSTD algorithm](https://facebook.github.io/zstd/), but again this is a compression layer on top of 
the underlying MTF format.

## Multi-file backups

The nomenclature around backups is a bit strange due to the lineage of the file format. Media can be other sources, but for the purposes of the backup source type it will always be files.

- `Media Set` - set of `Media Family` (files) that make up a backup
- `Media Family` - a single file that is part of a `Media Set`

When multiple files are used for a backup, pages are "striped" across the files. The striping is similar to RAID, where different types can striped, mirrored, or stripe-mirrored.