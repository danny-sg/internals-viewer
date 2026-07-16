# Timeline

The timeline shows database events over the lifetime of the query.

It is split into **bands**:

- Read
- Lock
- Latch
- Wait

The bands can be split into **lanes** that further add categories to the timeline events.

## Read

The Read band shows read operations. This is where the database is retrieving pages.

> [!CONCEPT]
> The _Buffer Pool_ is how SQL Server manages pages in-memory.
>
> The PFS can be added to the allocation map by toggling the PFS toolbar button.
